using System.Net;
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
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly JwtHelper _jwt;

    private static readonly HashSet<string> PublicFunctions =
    [
        "HashPassword",
        "ValidateUser"
    ];

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        JwtHelper jwt)
    {
        _logger = logger;
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
                _logger.LogWarning("Petición entrante sin token");
                await WriteUnauthorized(context, req, "Missing Authorization header");
                return;
            }

            var header = values.FirstOrDefault();
            if (header is null || !header.StartsWith("Bearer "))
            {
                _logger.LogWarning("Petición entrante sin token");
                await WriteUnauthorized(context, req, "Invalid Authorization header");
                return;
            }

            var token = header["Bearer ".Length..];

            var principal = _jwt.Validate(token);

            context.Items["User"] = principal;

            await next(context);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Invalid or expired JWT");
            await WriteUnauthorized(context, null, "Invalid or expired token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
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
