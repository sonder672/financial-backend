using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using FinancialApp.Backend.Security;
using FinancialApp.Backend.Util;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace FinancialApp.Backend.Middleware;

public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly JwtHelper _jwt;
    private static readonly HashSet<string> PublicFunctions =
    [
        "HashPassword",
        "ValidateUser"
    ];

    public ExceptionHandlingMiddleware(JwtHelper jwt)
    {
        _jwt = jwt;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            var req = await context.GetHttpRequestDataAsync();
            
            if (req is null)
            {
                await next(context);
                return;
            }

            var functionName = context.FunctionDefinition.Name;

            if (PublicFunctions.Contains(functionName))
            {
                await next(context);
                return;
            }

            if (!req.Headers.TryGetValues("Authorization", out var values))
            {
                context.GetLogger("ExceptionHandlingMiddleware").LogWarning("Petición entrante sin token");
                await WriteUnauthorized(context, req, "Missing Authorization header");
                return;
            }

            var header = values.FirstOrDefault();
            if (header is null || !header.StartsWith("Bearer "))
            {
                context.GetLogger("ExceptionHandlingMiddleware").LogWarning("Petición entrante sin token");
                await WriteUnauthorized(context, req, "Invalid Authorization header");
                return;
            }

            var token = header["Bearer ".Length..];

            var principal = _jwt.Validate(token);
            string? userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrWhiteSpace(userId))
            {
                context.GetLogger("ExceptionHandlingMiddleware").LogWarning("Claim userId vacío: {jwt}", token);
                await WriteError(context);

                return;
            }

            context.Items["UserId"] = userId;

            await next(context);
        }
        catch (SecurityTokenException ex)
        {
            context.GetLogger("ExceptionHandlingMiddleware").LogWarning(ex, "JWT inválido o expirado");
            await WriteUnauthorized(context, null, "Invalid or expired token");
        }
        catch (Exception ex)
        {
            context.GetLogger("ExceptionHandlingMiddleware").LogWarning(ex, "Excepción genérica");
            await WriteError(context);
        }
    }

    private static async Task WriteUnauthorized(
        FunctionContext context,
        HttpRequestData? req,
        string message)
    {
        req ??= await context.GetHttpRequestDataAsync();
        if (req is null) return;

        HttpResponseData res = await JsonResponse
            .Create(req, HttpStatusCode.Unauthorized, message);

        context.GetInvocationResult().Value = res;
    }

    private static async Task WriteError(FunctionContext context)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is null) return;

         HttpResponseData res = await JsonResponse
            .Create(req, HttpStatusCode.InternalServerError, "Internal server error");

        context.GetInvocationResult().Value = res;
    }
}
