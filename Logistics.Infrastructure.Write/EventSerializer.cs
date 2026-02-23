using System.Collections.Generic;
using System.Text.Json;
using Logistics.Domain;

namespace Logistics.Infrastructure.Write;

public sealed class EventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Dictionary<string, Type> EventTypeMap = new(StringComparer.Ordinal)
    {
        [nameof(OrderPlaced)] = typeof(OrderPlaced),
        [nameof(OrderPacked)] = typeof(OrderPacked),
        [nameof(OrderShipped)] = typeof(OrderShipped),
        [nameof(DeliveryAddressChanged)] = typeof(DeliveryAddressChanged),
        [nameof(DeliveryAttemptFailed)] = typeof(DeliveryAttemptFailed),
        [nameof(OrderDelivered)] = typeof(OrderDelivered),
    };

    public string Serialize(object evt)
    {
        return JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);
    }

    public object Deserialize(string eventType, string json)
    {
        if (!EventTypeMap.TryGetValue(eventType, out var type))
        {
            throw new InvalidOperationException($"Unknown event type '{eventType}'.");
        }

        var result = JsonSerializer.Deserialize(json, type, JsonOptions);
        return result ?? throw new InvalidOperationException($"Failed to deserialize event '{eventType}'.");
    }
}
