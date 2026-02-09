using System.Net;
using FinancialApp.Backend.Security;
using FinancialApp.Backend.Util;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace FinancialApp.Backend.Functions.Http;

public class HashPasswordFunction
{
    private readonly ILogger<HashPasswordFunction> _logger;
    private readonly Container _container;

    public HashPasswordFunction(ILogger<HashPasswordFunction> logger, CosmosClient client)
    {
        _logger = logger;
        _container = client.GetContainer("financeDb", "users");;
    }

    [Function("HashPassword")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "post")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JObject.Parse(body);

        string? password = null;
        string? email = null;

        if (data.TryGetValue("password", StringComparison.OrdinalIgnoreCase, out JToken? passwordToken))
        {
            password = passwordToken?.ToString();
        }

        if (data.TryGetValue("email", StringComparison.OrdinalIgnoreCase, out JToken? emailToken))
        {
            email = emailToken?.ToString();
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("No se envió contraseña para emitir hash");

            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Password is required");
            return bad;
        }

        var (hash, salt) = PasswordHasher.Hash(password);

        if (string.IsNullOrWhiteSpace(email))
        {
            var response = new
            {
                passwordHash = hash,
                passwordSalt = salt,
                algorithm = "PBKDF2-SHA256"
            };

            return await JsonResponse
                .Create(req, HttpStatusCode.OK, response);
        }
       
        try
        {
            var user = new Models.User
            {
                Id = email,
                Email = email,
                PasswordHash = hash, 
                PasswordSalt = salt  
            };

            await _container
                .CreateItemAsync(user, new PartitionKey(user.Email));

            _logger.LogInformation("Usuario {user} creado correctamente", email);

            return await JsonResponse
                .Create(req, HttpStatusCode.OK);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error durante la creación del usuario {usuario}", email);

            return await JsonResponse
                .Create(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }
}
