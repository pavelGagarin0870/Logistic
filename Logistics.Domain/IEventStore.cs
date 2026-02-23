namespace Logistics.Domain;

public interface IEventStore
{
    Task AppendEventsAsync(Guid aggregateId, IEnumerable<object> events, CancellationToken ct = default);

    Task<IReadOnlyList<object>> GetEventsAsync(Guid aggregateId, CancellationToken ct = default);

    Task<IReadOnlyList<(long GlobalSequence, object Event)>> GetEventsSinceAsync(
        long fromExclusiveGlobalSequence,
        int maxCount,
        CancellationToken ct = default);
}
