namespace Logistics.Domain;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected (e.g. duplicate AggregateId + Version in the event store).
/// </summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException()
    {
    }

    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
