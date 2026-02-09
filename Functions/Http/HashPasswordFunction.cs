using System.Net;
using FinancialApp.Backend.Security;
using FinancialApp.Backend.Util;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinancialApp.Backend.Functions.Http;

public class HashPasswordFunction
{
    private readonly ILogger<HashPasswordFunction> _logger;

    public HashPasswordFunction(ILogger<HashPasswordFunction> logger)
    {
        _logger = logger;
    }

    [Function("HashPassword")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "post")] HttpRequestData req)
    {
        var password = await new StreamReader(req.Body).ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("No se envió contraseña para emitir hash");

            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Password is required");
            return bad;
        }

        var (hash, salt) = PasswordHasher.Hash(password);

        var response = new
        {
            passwordHash = hash,
            passwordSalt = salt,
            algorithm = "PBKDF2-SHA256"
        };

        return await JsonResponse
            .Create(req, HttpStatusCode.OK, response);
    }
}
