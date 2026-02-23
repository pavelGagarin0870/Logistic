using Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Infrastructure.Write;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWriteInfrastructure(
        this IServiceCollection services,
        string writeDbConnectionString)
    {
        services.AddDbContext<EventStoreDbContext>(options =>
            options.UseNpgsql(writeDbConnectionString));
        services.AddScoped<IEventStore, EventStore>();
        services.AddSingleton<IEventSerializer, EventSerializer>();
        return services;
    }
}
