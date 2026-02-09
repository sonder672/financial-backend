using System.Net;
using System.Text.Json;
using FinancialApp.Backend.Security;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinancialApp.Backend.Functions.Http;

public class ValidateUserFunction
{
    private readonly Container _container;
    private readonly ILogger<ValidateUserFunction> _logger;
    private readonly JwtHelper _jwt;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

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
            login = JsonSerializer.Deserialize<LoginRequest>(body, JsonOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, $"Ocurrió un error deserializando la entrada: {exception.Message}");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON");

            return bad;
        }

        if (login is null ||
            string.IsNullOrWhiteSpace(login.Email) ||
            string.IsNullOrWhiteSpace(login.Password))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Email and password are required");
            return bad;
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

            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Invalid credentials");
            return unauthorized;
        }

        var valid = PasswordHasher.Verify(
            login.Password,
            user.PasswordHash,
            user.PasswordSalt
        );

        if (!valid)
        {
            _logger.LogWarning("Contraseña inválida para {email}", login.Email);

            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Invalid credentials");
            return unauthorized;
        }

        var token = _jwt.GenerateToken(
            userId: user.Id,
            email: login.Email
        );

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            access_token = token,
            expires_in_minutes = int.Parse(
                req.FunctionContext
                   .InstanceServices
                   .GetRequiredService<IConfiguration>()["Jwt:ExpiresMinutes"]!)
        });

        return response;
    }
}

public record LoginRequest(string Email, string Password);