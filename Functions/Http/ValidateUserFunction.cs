using System.Net;
using FinancialApp.Backend.Security;
using FinancialApp.Backend.Util;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FinancialApp.Backend.Functions.Http;

public class ValidateUserFunction
{
    private readonly Container _container;
    private readonly ILogger<ValidateUserFunction> _logger;
    private readonly JwtHelper _jwt;

    private static readonly JsonSerializerSettings JsonOptions = new()
    {
        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
        {
            NamingStrategy = new Newtonsoft.Json.Serialization.DefaultNamingStrategy()
        }
    };
    public ValidateUserFunction(
        CosmosClient client,
        ILogger<ValidateUserFunction> logger,
        JwtHelper jwt)
    {
        _container = client.GetContainer("financeDb", "users");
        _logger = logger;
        _jwt = jwt;
    }

    [Function("ValidateUser")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        LoginRequest? login;

        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            login = JsonConvert.DeserializeObject<LoginRequest>(body, JsonOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Datos del Movimiento erróneos. Probablemente se envió en formato incorrecto o faltan parámetros.");

            return await JsonResponse
                .Create(req, HttpStatusCode.BadRequest, "Invalid JSON body");
        }

        if (login is null ||
            string.IsNullOrWhiteSpace(login.Email) ||
            string.IsNullOrWhiteSpace(login.Password))
        {
            return await JsonResponse
                .Create(req, HttpStatusCode.BadRequest, "Email and password are required");
        }

        Models.User? user;

        try
        {
            user = await _container.ReadItemAsync<Models.User>(
                login.Email,
                new PartitionKey(login.Email));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Usuario no encontrado: {email}", login.Email);

            return await JsonResponse
                .Create(req, HttpStatusCode.NotFound, "User not found");
        }

        var valid = PasswordHasher.Verify(
            login.Password,
            user.PasswordHash,
            user.PasswordSalt
        );

        if (!valid)
        {
            _logger.LogWarning("Contraseña inválida para {email}", login.Email);

            return await JsonResponse
                .Create(req, HttpStatusCode.NotFound, "User not found");
        }

        var token = _jwt.GenerateToken(
            userId: user.Id,
            email: login.Email
        );

        var response = new
        {
            access_token = token,
            expires_in_minutes = int.Parse(
                req.FunctionContext
                   .InstanceServices
                   .GetRequiredService<IConfiguration>()["Jwt:ExpiresMinutes"]!)
        };

        return await JsonResponse
                .Create(req, HttpStatusCode.OK, response);
    }
}

public record LoginRequest(string Email, string Password);