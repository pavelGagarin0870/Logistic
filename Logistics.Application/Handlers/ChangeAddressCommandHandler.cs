using Logistics.Application.Commands;
using Logistics.Application.DispatchR;
using Logistics.Domain;

namespace Logistics.Application.Handlers;

public sealed class ChangeAddressCommandHandler : IRequestHandler<ChangeAddressCommand, Unit>
{
    private readonly IEventStore _eventStore;

    public ChangeAddressCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<Unit> Handle(ChangeAddressCommand request, CancellationToken cancellationToken)
    {
        var events = await _eventStore.GetEventsAsync(request.OrderId, cancellationToken);
        if (events.Count == 0)
            throw new OrderNotFoundException(request.OrderId);

        var aggregate = OrderAggregate.LoadFromHistory(events);
        aggregate.ChangeAddress(request.NewAddress);

        var newEvents = aggregate.DequeueUncommittedEvents();
        await _eventStore.AppendEventsAsync(request.OrderId, newEvents, cancellationToken);

        return Unit.Value;
    }
}
