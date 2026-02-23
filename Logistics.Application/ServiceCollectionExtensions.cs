using Logistics.Application.Commands;
using Logistics.Application.DispatchR;
using Logistics.Application.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationDispatchR(this IServiceCollection services)
    {
        services.AddScoped<IDispatchR, DispatchR.DispatchR>();
        services.AddScoped<IRequestHandler<PackOrderCommand, Unit>, PackOrderCommandHandler>();
        services.AddScoped<IRequestHandler<FailDeliveryCommand, Unit>, FailDeliveryCommandHandler>();
        services.AddScoped<IRequestHandler<ChangeAddressCommand, Unit>, ChangeAddressCommandHandler>();
        services.AddScoped<IRequestHandler<PlaceOrderCommand, Unit>, PlaceOrderCommandHandler>();
        services.AddScoped<IRequestHandler<ShipOrderCommand, Unit>, ShipOrderCommandHandler>();
        services.AddScoped<IRequestHandler<MarkOrderDeliveredCommand, Unit>, MarkOrderDeliveredCommandHandler>();
        return services;
    }
}
