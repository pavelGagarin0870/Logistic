using Logistics.Application.Commands;
using Logistics.Application.DispatchR;
using Logistics.Domain;

namespace Logistics.Application.Handlers;

public sealed class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, Unit>
{
    private readonly IEventStore _eventStore;

    public ShipOrderCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<Unit> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        var events = await _eventStore.GetEventsAsync(request.OrderId, cancellationToken);
        if (events.Count == 0)
            throw new OrderNotFoundException(request.OrderId);

        var aggregate = OrderAggregate.LoadFromHistory(events);
        aggregate.ShipOrder(request.CourierName);

        var newEvents = aggregate.DequeueUncommittedEvents();
        await _eventStore.AppendEventsAsync(request.OrderId, newEvents, cancellationToken);

        return Unit.Value;
    }
}
