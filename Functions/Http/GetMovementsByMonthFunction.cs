using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Web;
using FinancialApp.Backend.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinancialApp.Backend.Functions.Http;

public class GetMovementsByMonthFunction
{
    private readonly Container _container;
    private readonly ILogger<GetMovementsByMonthFunction> _logger;

    public GetMovementsByMonthFunction(
        CosmosClient client,
        ILogger<GetMovementsByMonthFunction> logger)
    {
        _container = client.GetContainer("financeDb", "movements");
        _logger = logger;
    }

    [Function("GetMovementsByMonth")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req, FunctionContext context)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);

        var monthRaw = query["month"];

        if (!int.TryParse(monthRaw, out var month) || month < 1 || month > 12)
        {
            _logger.LogWarning("Mes inválido, debe estar entre 1-12: {Month}", monthRaw);

            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("month must be a number between 1 and 12");
            return bad;
        }

        var principal = (ClaimsPrincipal)context.Items["User"]!;
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;

        _logger.LogInformation(
            "Consultando movimientos de userId {UserId} and month {Month}",
            userId, month);

        var queryDefinition = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId AND MONTH(c.date) = @month")
            .WithParameter("@userId", userId)
            .WithParameter("@month", month);

        var iterator = _container.GetItemQueryIterator<Movement>(queryDefinition);
        var results = new List<Movement>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(results);
        return ok;
    }
}
