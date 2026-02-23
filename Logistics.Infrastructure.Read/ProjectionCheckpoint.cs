namespace Logistics.Infrastructure.Read;

public sealed class ProjectionCheckpoint
{
    public string ProjectionName { get; set; } = string.Empty;
    public long LastProcessedGlobalSequence { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
