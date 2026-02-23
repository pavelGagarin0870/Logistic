namespace Logistics.Domain;

public sealed class OrderAggregate
{
    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public decimal Total { get; private set; }

    public string? WarehouseId { get; private set; }
    public double? Weight { get; private set; }
    public string? CourierName { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.None;
    public bool IsPacked => Status >= OrderStatus.Packed;
    public bool IsShipped => Status >= OrderStatus.Shipped;
    public bool IsDelivered => Status == OrderStatus.Delivered;
    public bool IsFailed => Status == OrderStatus.Failed;

    private readonly List<object> _uncommittedEvents = new();

    private OrderAggregate()
    {
    }

    public static OrderAggregate Create(OrderPlaced @event)
    {
        var aggregate = new OrderAggregate();
        aggregate.ApplyChange(@event);
        return aggregate;
    }

    public static OrderAggregate LoadFromHistory(IEnumerable<object> events)
    {
        var aggregate = new OrderAggregate();

        foreach (var @event in events)
        {
            aggregate.Apply(@event);
        }

        return aggregate;
    }

    public IReadOnlyCollection<object> DequeueUncommittedEvents()
    {
        var events = _uncommittedEvents.ToArray();
        _uncommittedEvents.Clear();
        return events;
    }

    public void PackOrder(string warehouseId, double weight)
    {
        if (Status == OrderStatus.None)
        {
            throw new InvalidOrderStateException("Order must be created before it can be packed.");
        }

        if (IsShipped)
        {
            throw new InvalidOrderStateException("Cannot pack an order that has already been shipped.");
        }

        if (IsPacked)
        {
            throw new InvalidOrderStateException("Order is already packed.");
        }

        ApplyChange(new OrderPacked(Id, warehouseId, weight));
    }

    public void ShipOrder(string courierName)
    {
        if (!IsPacked)
        {
            throw new InvalidOrderStateException("Order must be packed before it can be shipped.");
        }

        if (IsShipped)
        {
            throw new InvalidOrderStateException("Order is already shipped.");
        }

        ApplyChange(new OrderShipped(Id, courierName));
    }

    public void ChangeAddress(string newAddress)
    {
        if (IsShipped)
        {
            throw new InvalidOrderStateException("Delivery address cannot be changed after the order is shipped.");
        }

        ApplyChange(new DeliveryAddressChanged(Id, newAddress));
    }

    public void FailDelivery(string reason)
    {
        if (!IsShipped)
        {
            throw new InvalidOrderStateException("Delivery attempt can only fail when the order is shipped.");
        }

        if (IsDelivered || IsFailed)
        {
            throw new InvalidOrderStateException("Delivery attempt cannot fail once the order is delivered or already failed.");
        }

        ApplyChange(new DeliveryAttemptFailed(Id, reason));
    }

    public void MarkDelivered(DateTime deliveredAt)
    {
        if (!IsShipped)
        {
            throw new InvalidOrderStateException("Order must be shipped before it can be marked as delivered.");
        }

        if (IsDelivered)
        {
            throw new InvalidOrderStateException("Order is already delivered.");
        }

        if (IsFailed)
        {
            throw new InvalidOrderStateException("Order delivery has already failed.");
        }

        ApplyChange(new OrderDelivered(Id, deliveredAt));
    }

    private void ApplyChange(object @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    private void Apply(object @event)
    {
        switch (@event)
        {
            case OrderPlaced e:
                When(e);
                break;
            case OrderPacked e:
                When(e);
                break;
            case OrderShipped e:
                When(e);
                break;
            case DeliveryAddressChanged e:
                When(e);
                break;
            case DeliveryAttemptFailed e:
                When(e);
                break;
            case OrderDelivered e:
                When(e);
                break;
            default:
                throw new InvalidOperationException($"Unknown event type '{@event.GetType().Name}'.");
        }
    }

    private void When(OrderPlaced e)
    {
        Id = e.OrderId;
        CustomerName = e.CustomerName;
        Address = e.Address;
        Total = e.Total;
        Status = OrderStatus.Placed;
    }

    private void When(OrderPacked e)
    {
        WarehouseId = e.WarehouseId;
        Weight = e.Weight;
        Status = OrderStatus.Packed;
    }

    private void When(OrderShipped e)
    {
        CourierName = e.CourierName;
        Status = OrderStatus.Shipped;
    }

    private void When(DeliveryAddressChanged e)
    {
        Address = e.NewAddress;
    }

    private void When(DeliveryAttemptFailed e)
    {
        Status = OrderStatus.Failed;
    }

    private void When(OrderDelivered e)
    {
        Status = OrderStatus.Delivered;
    }
}
