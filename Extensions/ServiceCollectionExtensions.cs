using FinancialApp.Backend.Security;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialApp.Backend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Addservices(this IServiceCollection services)
    {
        services.AddSingleton<JwtHelper>();
        services.AddSingleton<PasswordHasher>();
        
        return services;
    }
}
