using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Infrastructure.Read;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReadInfrastructure(
        this IServiceCollection services,
        string readDbConnectionString)
    {
        services.AddDbContext<ReadDbContext>(options =>
            options.UseNpgsql(readDbConnectionString));
        return services;
    }
}
