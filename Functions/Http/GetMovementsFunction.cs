using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using FinancialApp.Backend.Models;
using FinancialApp.Backend.Util;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FinancialApp.Backend.Functions.Http;

public class GetMovementsFunction
{
    private readonly Container _container;

    public GetMovementsFunction(
        CosmosClient client)
    {
        _container = client.GetContainer("financeDb", "movements");
    }

    [Function("GetMovements")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
        FunctionContext context)
    {
        var principal = (ClaimsPrincipal)context.Items["User"]!;
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId")
            .WithParameter("@userId", userId);

        var results = new List<Movement>();
        using var iterator = _container.GetItemQueryIterator<Movement>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return await JsonResponse
            .Create(req, HttpStatusCode.OK, results);
    }
}
