using System.Text;
using System.Text.Json;
using Logistics.Domain;
using Logistics.Infrastructure.Read;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var readConnection = builder.Configuration.GetConnectionString("ReadDbConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:ReadDbConnection is required.");
builder.Services.AddReadInfrastructure(readConnection);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<RabbitMqPublisher>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/orders/{id:guid}", async (Guid id, ReadDbContext db, CancellationToken ct) =>
{
    var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == id, ct);
    if (order == null)
        return Results.NotFound();

    var historyOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var history = string.IsNullOrWhiteSpace(order.StatusHistoryJson) || order.StatusHistoryJson == "[]"
        ? Array.Empty<OrderStatusHistoryItem>()
        : System.Text.Json.JsonSerializer.Deserialize<OrderStatusHistoryItem[]>(order.StatusHistoryJson, historyOptions) ?? Array.Empty<OrderStatusHistoryItem>();

    var status = Enum.TryParse<OrderStatus>(order.Status, true, out var parsed)
        ? parsed
        : OrderStatus.None;

    return Results.Ok(new OrderDetailsDto(
        order.OrderId,
        order.CustomerName,
        order.Address,
        order.Total,
        status,
        order.WarehouseId,
        order.Weight,
        order.CourierName,
        order.CreatedAtUtc,
        order.ShippedAtUtc,
        order.DeliveredAtUtc,
        history
    ));
});

app.MapGet("/api/orders/failed", async (ReadDbContext db, CancellationToken ct) =>
{
    var today = DateTime.UtcNow.Date;
    var tomorrow = today.AddDays(1);
    var list = await db.ProblematicOrders
        .AsNoTracking()
        .Where(p => p.FailedAtUtc >= today && p.FailedAtUtc < tomorrow)
        .Select(p => new FailedOrderDto(p.OrderId, p.CustomerName, p.Address, p.Reason, p.FailedAtUtc))
        .ToListAsync(ct);
    return Results.Ok(list);
});

app.MapPost("/api/orders", async (CreateOrderRequest request, RabbitMqPublisher publisher, CancellationToken ct) =>
{
    var orderId = request.OrderId ?? Guid.NewGuid();
    var payload = new PlaceOrderMessage(orderId, request.CustomerName, request.Address, request.Total);
    await publisher.PublishCommandAsync("PlaceOrder", payload, ct);
    return Results.Accepted($"/api/orders/{orderId}", new { OrderId = orderId });
});

app.MapPost("/api/orders/{id:guid}/pack", async (Guid id, PackOrderRequest request, RabbitMqPublisher publisher, CancellationToken ct) =>
{
    var payload = new PackOrderMessage(id, request.WarehouseId, request.Weight);
    await publisher.PublishCommandAsync("PackOrder", payload, ct);
    return Results.Accepted($"/api/orders/{id}");
});

app.MapPost("/api/orders/{id:guid}/change-address", async (Guid id, ChangeAddressRequest request, RabbitMqPublisher publisher, CancellationToken ct) =>
{
    var payload = new ChangeAddressMessage(id, request.NewAddress);
    await publisher.PublishCommandAsync("ChangeAddress", payload, ct);
    return Results.Accepted($"/api/orders/{id}");
});

app.MapPost("/api/orders/{id:guid}/fail-delivery", async (Guid id, FailDeliveryRequest request, RabbitMqPublisher publisher, CancellationToken ct) =>
{
    var payload = new FailDeliveryMessage(id, request.Reason);
    await publisher.PublishCommandAsync("FailDelivery", payload, ct);
    return Results.Accepted($"/api/orders/{id}");
});

await app.RunAsync();

public record OrderDetailsDto(
    Guid OrderId,
    string CustomerName,
    string Address,
    decimal Total,
    OrderStatus Status,
    string? WarehouseId,
    double? Weight,
    string? CourierName,
    DateTime CreatedAtUtc,
    DateTime? ShippedAtUtc,
    DateTime? DeliveredAtUtc,
    OrderStatusHistoryItem[] StatusHistory
);

public record OrderStatusHistoryItem(string Status, DateTime At);

public record FailedOrderDto(Guid OrderId, string CustomerName, string Address, string Reason, DateTime FailedAtUtc);

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    /// <summary>Single queue for all order commands.</summary>
    public string CommandQueue { get; set; } = "order-commands";
}

public sealed class RabbitMqPublisher : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    public RabbitMqOptions Options { get; }

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options)
    {
        Options = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = Options.HostName,
            Port = Options.Port,
            UserName = Options.UserName,
            Password = Options.Password
        };
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
    }

    /// <summary>Publishes a command to the single command queue using an envelope (commandType + payload).</summary>
    public Task PublishCommandAsync(string commandType, object payload, CancellationToken cancellationToken = default)
    {
        var envelope = new { commandType, payload };
        var json = JsonSerializer.Serialize(envelope);
        var body = Encoding.UTF8.GetBytes(json);
        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };
        return _channel.BasicPublishAsync(exchange: string.Empty, routingKey: Options.CommandQueue, mandatory: false, basicProperties: props, body: body, cancellationToken: cancellationToken).AsTask();
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}

public record CreateOrderRequest(Guid? OrderId, string CustomerName, string Address, decimal Total);

public record PackOrderRequest(string WarehouseId, double Weight);

public record ChangeAddressRequest(string NewAddress);

public record FailDeliveryRequest(string Reason);

// Messages shaped to match command records in the write side
public record PlaceOrderMessage(Guid OrderId, string CustomerName, string Address, decimal Total);

public record PackOrderMessage(Guid OrderId, string WarehouseId, double Weight);

public record ChangeAddressMessage(Guid OrderId, string NewAddress);

public record FailDeliveryMessage(Guid OrderId, string Reason);
