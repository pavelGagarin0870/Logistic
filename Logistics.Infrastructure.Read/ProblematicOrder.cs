namespace Logistics.Infrastructure.Read;

public sealed class ProblematicOrder
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime FailedAtUtc { get; set; }
}
