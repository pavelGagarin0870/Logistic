namespace Logistics.Infrastructure.Read;

public sealed class OrderDetailsView
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? WarehouseId { get; set; }
    public double? Weight { get; set; }
    public string? CourierName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ShippedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? LastStatusChangeAtUtc { get; set; }
    public string StatusHistoryJson { get; set; } = "[]";
}
