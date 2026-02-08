using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using FinancialApp.Backend.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinancialApp.Backend.Functions.Http;

public class DeleteMovementFunction
{
    private readonly Container _container;
    private readonly ILogger<DeleteMovementFunction> _logger;

    public DeleteMovementFunction(
        CosmosClient client,
        ILogger<DeleteMovementFunction> logger)
    {
        _container = client.GetContainer("financeDb", "movements");
        _logger = logger;
    }

    [Function("DeleteMovement")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequestData req, FunctionContext context)
    {
        _logger.LogInformation("DeleteMovement triggered");

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var id = query["id"];

        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("Faltan parámetros para eliminar el movimiento. id: {Id}", id);

            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("id and userId are required");
            return bad;
        }

        var principal = (ClaimsPrincipal)context.Items["User"]!;
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;

        await _container.DeleteItemAsync<Movement>(
            id,
            new PartitionKey(userId));

        _logger.LogInformation(
            "Movement deleted successfully. id: {Id}, userId: {UserId}",
            id,
            userId);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Movement deleted");
        return response;
    }
}
