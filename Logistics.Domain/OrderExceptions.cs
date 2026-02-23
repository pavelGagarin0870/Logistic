namespace Logistics.Domain;

public sealed class OrderNotFoundException : Exception
{
    public OrderNotFoundException(Guid orderId)
        : base($"Order with id '{orderId}' was not found.")
    {
    }
}

public sealed class InvalidOrderStateException : Exception
{
    public InvalidOrderStateException(string message) : base(message)
    {
    }
}

public sealed class OrderAlreadyPackedException : Exception
{
    public OrderAlreadyPackedException(Guid orderId)
        : base($"Order '{orderId}' is already packed.") { }
}

public sealed class OrderNotPackedException : Exception
{
    public OrderNotPackedException(Guid orderId)
        : base($"Order '{orderId}' must be packed before it can be shipped.") { }
}

public sealed class OrderAlreadyShippedException : Exception
{
    public OrderAlreadyShippedException(Guid orderId)
        : base($"Order '{orderId}' is already shipped.") { }
}

public sealed class OrderAlreadyDeliveredException : Exception
{
    public OrderAlreadyDeliveredException(Guid orderId)
        : base($"Order '{orderId}' is already delivered or delivery has failed.") { }
}
