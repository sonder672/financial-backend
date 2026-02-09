using FinancialApp.Backend.Security;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialApp.Backend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Addservices(this IServiceCollection services)
    {
        services.AddSingleton<JwtHelper>();
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton((service) =>
        {
            var configuration = service.GetRequiredService<IConfiguration>();
            string? cosmosDbConnection = configuration["CosmosDbConnection"];

            if (string.IsNullOrWhiteSpace(cosmosDbConnection))
            {
                throw new ArgumentNullException(cosmosDbConnection, $"Cadena de conexión vacía");
            }

            return new CosmosClient(cosmosDbConnection);
        });
        
        return services;
    }
}
