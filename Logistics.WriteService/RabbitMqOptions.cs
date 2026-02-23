namespace Logistics.WriteService;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    /// <summary>Single queue for all order commands (place, pack, change-address, fail-delivery).</summary>
    public string CommandQueue { get; set; } = "order-commands";
}

/// <summary>Envelope for messages on the single command queue. Payload is deserialized per CommandType.</summary>
public sealed class CommandEnvelope
{
    public string CommandType { get; set; } = string.Empty;
    public System.Text.Json.JsonElement Payload { get; set; }
}
