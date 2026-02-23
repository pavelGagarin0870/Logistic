namespace Logistics.Domain;

public enum OrderStatus
{
    None = 0,
    Placed = 1,
    Packed = 2,
    Shipped = 3,
    Failed = 4,
    Delivered = 5
}

public record OrderPlaced(Guid OrderId, string CustomerName, string Address, decimal Total);

public record OrderPacked(Guid OrderId, string WarehouseId, double Weight);

public record OrderShipped(Guid OrderId, string CourierName);

public record DeliveryAddressChanged(Guid OrderId, string NewAddress);

public record DeliveryAttemptFailed(Guid OrderId, string Reason);

public record OrderDelivered(Guid OrderId, DateTime DeliveredAt);
