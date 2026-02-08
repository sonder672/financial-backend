using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FinancialApp.Backend.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinancialApp.Backend.Functions.Http;

public class CreateMovementFunction
{
    private readonly Container _container;
    private readonly ILogger<CreateMovementFunction> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CreateMovementFunction(
        CosmosClient client,
        ILogger<CreateMovementFunction> logger)
    {
        _container = client.GetContainer("financeDb", "movements");
        _logger = logger;
    }

    [Function("CreateMovement")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, FunctionContext context)
    {
        Movement? movement;
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();

            movement = JsonSerializer.Deserialize<Movement>(
                body,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Datos del Movimiento erróneos");

            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON body");
            return bad;
        }

        if (movement is null)
        {
            _logger.LogWarning("Movement body is null");

            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Movement data is required");
            return bad;
        }

        var principal = (ClaimsPrincipal)context.Items["User"]!;
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;

            movement.UserId = userId;

        movement.Date = movement.Date == default 
            ? DateTime.UtcNow 
            : movement.Date;

        try
        {
            await _container.CreateItemAsync(
                movement,
                new PartitionKey(movement.UserId));
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error durante la creación del movimiento");

            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Error saving movement");
            return error;
        }

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(movement);
        return response;
    }
}
