namespace Logistics.Infrastructure.Write;

public sealed class EventRecord
{
    public long GlobalSequence { get; set; }
    public Guid AggregateId { get; set; }
    public int Version { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
