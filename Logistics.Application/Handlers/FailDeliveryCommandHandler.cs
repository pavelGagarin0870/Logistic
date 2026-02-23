using Logistics.Application.Commands;
using Logistics.Application.DispatchR;
using Logistics.Domain;

namespace Logistics.Application.Handlers;

public sealed class FailDeliveryCommandHandler : IRequestHandler<FailDeliveryCommand, Unit>
{
    private readonly IEventStore _eventStore;

    public FailDeliveryCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<Unit> Handle(FailDeliveryCommand request, CancellationToken cancellationToken)
    {
        var events = await _eventStore.GetEventsAsync(request.OrderId, cancellationToken);
        if (events.Count == 0)
            throw new OrderNotFoundException(request.OrderId);

        var aggregate = OrderAggregate.LoadFromHistory(events);
        aggregate.FailDelivery(request.Reason);

        var newEvents = aggregate.DequeueUncommittedEvents();
        await _eventStore.AppendEventsAsync(request.OrderId, newEvents, cancellationToken);

        return Unit.Value;
    }
}
