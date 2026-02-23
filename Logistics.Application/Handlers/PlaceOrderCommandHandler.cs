using Logistics.Application.Commands;
using Logistics.Application.DispatchR;
using Logistics.Domain;

namespace Logistics.Application.Handlers;

public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Unit>
{
    private readonly IEventStore _eventStore;

    public PlaceOrderCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<Unit> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var events = await _eventStore.GetEventsAsync(request.OrderId, cancellationToken);
        if (events.Count > 0)
            throw new InvalidOrderStateException($"Order '{request.OrderId}' already exists.");

        var placed = new OrderPlaced(request.OrderId, request.CustomerName, request.Address, request.Total);
        var aggregate = OrderAggregate.Create(placed);

        var newEvents = aggregate.DequeueUncommittedEvents();
        await _eventStore.AppendEventsAsync(request.OrderId, newEvents, cancellationToken);

        return Unit.Value;
    }
}
