using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Application.DispatchR;

public sealed class DispatchR : IDispatchR
{
    private readonly IServiceProvider _serviceProvider;

    public DispatchR(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<Unit>
    {
        var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest, Unit>>();
        await handler.Handle(request, cancellationToken);
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
    {
        var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        return await handler.Handle(request, cancellationToken);
    }
}
