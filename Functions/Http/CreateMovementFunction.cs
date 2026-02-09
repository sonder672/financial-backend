using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FinancialApp.Backend.Models;
using FinancialApp.Backend.Util;
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
            _logger.LogWarning(ex, "Datos del Movimiento erróneos. Probablemente se envió en formato incorrecto o faltan parámetros.");

            return await JsonResponse
                .Create(req, HttpStatusCode.BadRequest, "Invalid JSON body");
        }

        if (movement is null)
        {
            _logger.LogWarning("Faltan parámetros para crear el movimiento {body}", req.Body);

            return await JsonResponse
                .Create(req, HttpStatusCode.BadRequest, "Movement data is required");
        }

        var principal = (ClaimsPrincipal)context.Items["User"]!;
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;

            movement.UserId = userId;

        movement.Date = movement.Date == default 
            ? ColombianDate.Today()
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

            return await JsonResponse
                .Create(req, HttpStatusCode.InternalServerError, "Error saving movement");
        }

        return await JsonResponse
                .Create(req, HttpStatusCode.Created, movement);
    }
}
