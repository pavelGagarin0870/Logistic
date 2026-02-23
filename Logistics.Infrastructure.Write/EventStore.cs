using Microsoft.EntityFrameworkCore;
using Npgsql;
using Logistics.Domain;

namespace Logistics.Infrastructure.Write;

public sealed class EventStore : IEventStore
{
    private readonly EventStoreDbContext _db;
    private readonly IEventSerializer _serializer;

    public EventStore(EventStoreDbContext db, IEventSerializer serializer)
    {
        _db = db;
        _serializer = serializer;
    }

    public async Task AppendEventsAsync(Guid aggregateId, IEnumerable<object> events, CancellationToken ct = default)
    {
        var list = events.ToList();
        if (list.Count == 0) return;

        var nextVersion = await _db.Events
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version, ct) ?? 0;

        var records = new List<EventRecord>();
        foreach (var evt in list)
        {
            nextVersion++;
            records.Add(new EventRecord
            {
                AggregateId = aggregateId,
                Version = nextVersion,
                EventType = evt.GetType().Name,
                Data = _serializer.Serialize(evt),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _db.Events.AddRangeAsync(records, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                throw new ConcurrencyException("A concurrent write conflicted with the same aggregate (unique constraint on AggregateId + Version).", ex);
            throw;
        }
    }

    public async Task<IReadOnlyList<object>> GetEventsAsync(Guid aggregateId, CancellationToken ct = default)
    {
        var records = await _db.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.GlobalSequence)
            .ToListAsync(ct);

        return records
            .Select(r => _serializer.Deserialize(r.EventType, r.Data))
            .ToList();
    }

    public async Task<IReadOnlyList<(long GlobalSequence, object Event)>> GetEventsSinceAsync(
        long fromExclusiveGlobalSequence,
        int maxCount,
        CancellationToken ct = default)
    {
        var records = await _db.Events
            .Where(e => e.GlobalSequence > fromExclusiveGlobalSequence)
            .OrderBy(e => e.GlobalSequence)
            .Take(maxCount)
            .ToListAsync(ct);

        return records
            .Select(r => (r.GlobalSequence, _serializer.Deserialize(r.EventType, r.Data)))
            .ToList();
    }
}
