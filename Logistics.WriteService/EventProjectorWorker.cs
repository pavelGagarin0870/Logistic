using System.Text.Json;
using Logistics.Domain;
using Logistics.Infrastructure.Read;
using Logistics.Infrastructure.Write;
using Microsoft.EntityFrameworkCore;

namespace Logistics.WriteService;

public sealed class EventProjectorWorker : BackgroundService
{
    private const string ProjectionName = "OrderProjections";
    private const int BatchSize = 500;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventProjectorWorker> _logger;

    private static readonly JsonSerializerOptions StatusHistoryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EventProjectorWorker(IServiceProvider serviceProvider, ILogger<EventProjectorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event projector worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunProjectionBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event projector batch.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RunProjectionBatchAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var writeDb = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        var readDb = scope.ServiceProvider.GetRequiredService<ReadDbContext>();

        var checkpoint = await readDb.ProjectionCheckpoints
            .FirstOrDefaultAsync(c => c.ProjectionName == ProjectionName, ct);

        if (checkpoint == null)
        {
            checkpoint = new ProjectionCheckpoint
            {
                ProjectionName = ProjectionName,
                LastProcessedGlobalSequence = 0,
                UpdatedAtUtc = DateTime.UtcNow
            };
            readDb.ProjectionCheckpoints.Add(checkpoint);
            await readDb.SaveChangesAsync(ct);
        }

        var eventsWithSequence = await eventStore.GetEventsSinceAsync(
            checkpoint.LastProcessedGlobalSequence,
            BatchSize,
            ct);

        if (eventsWithSequence.Count == 0)
            return;

        await using var transaction = await readDb.Database.BeginTransactionAsync(ct);

        try
        {
            long lastSequence = checkpoint.LastProcessedGlobalSequence;
            foreach (var (globalSequence, evt) in eventsWithSequence)
            {
                await ApplyEventAsync(readDb, evt, ct);
                lastSequence = globalSequence;
            }

            checkpoint.LastProcessedGlobalSequence = lastSequence;
            checkpoint.UpdatedAtUtc = DateTime.UtcNow;
            await readDb.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task ApplyEventAsync(ReadDbContext readDb, object evt, CancellationToken ct)
    {
        switch (evt)
        {
            case OrderPlaced e:
                readDb.Orders.Add(new OrderDetailsView
                {
                    OrderId = e.OrderId,
                    CustomerName = e.CustomerName,
                    Address = e.Address,
                    Total = e.Total,
                    Status = "Placed",
                    CreatedAtUtc = DateTime.UtcNow,
                    LastStatusChangeAtUtc = DateTime.UtcNow,
                    StatusHistoryJson = JsonSerializer.Serialize(new[] { new { Status = "Placed", At = DateTime.UtcNow } }, StatusHistoryJsonOptions)
                });
                break;
            case OrderPacked e:
                var packedView = await readDb.Orders.FindAsync([e.OrderId], ct);
                if (packedView != null)
                {
                    packedView.WarehouseId = e.WarehouseId;
                    packedView.Weight = e.Weight;
                    packedView.Status = "Packed";
                    packedView.LastStatusChangeAtUtc = DateTime.UtcNow;
                    packedView.StatusHistoryJson = AppendHistory(packedView.StatusHistoryJson, "Packed");
                }
                break;
            case OrderShipped e:
                var shippedView = await readDb.Orders.FindAsync([e.OrderId], ct);
                if (shippedView != null)
                {
                    shippedView.CourierName = e.CourierName;
                    shippedView.Status = "Shipped";
                    shippedView.ShippedAtUtc = DateTime.UtcNow;
                    shippedView.LastStatusChangeAtUtc = DateTime.UtcNow;
                    shippedView.StatusHistoryJson = AppendHistory(shippedView.StatusHistoryJson, "Shipped");
                }
                break;
            case DeliveryAddressChanged e:
                var addrView = await readDb.Orders.FindAsync([e.OrderId], ct);
                if (addrView != null)
                {
                    addrView.Address = e.NewAddress;
                    addrView.LastStatusChangeAtUtc = DateTime.UtcNow;
                }
                break;
            case DeliveryAttemptFailed e:
                var failedView = await readDb.Orders.FindAsync([e.OrderId], ct);
                if (failedView != null)
                {
                    failedView.Status = "Failed";
                    failedView.LastStatusChangeAtUtc = DateTime.UtcNow;
                    failedView.StatusHistoryJson = AppendHistory(failedView.StatusHistoryJson, "Failed");
                    var problematic = await readDb.ProblematicOrders.FindAsync([e.OrderId], ct);
                    var now = DateTime.UtcNow;
                    if (problematic != null)
                    {
                        problematic.Reason = e.Reason;
                        problematic.FailedAtUtc = now;
                    }
                    else
                    {
                        readDb.ProblematicOrders.Add(new ProblematicOrder
                        {
                            OrderId = e.OrderId,
                            CustomerName = failedView.CustomerName,
                            Address = failedView.Address,
                            Reason = e.Reason,
                            FailedAtUtc = now
                        });
                    }
                }
                break;
            case OrderDelivered e:
                var deliveredView = await readDb.Orders.FindAsync([e.OrderId], ct);
                if (deliveredView != null)
                {
                    deliveredView.Status = "Delivered";
                    deliveredView.DeliveredAtUtc = e.DeliveredAt;
                    deliveredView.LastStatusChangeAtUtc = DateTime.UtcNow;
                    deliveredView.StatusHistoryJson = AppendHistory(deliveredView.StatusHistoryJson, "Delivered");
                    var problematicOrder = await readDb.ProblematicOrders.FindAsync([e.OrderId], ct);
                    if (problematicOrder != null)
                        readDb.ProblematicOrders.Remove(problematicOrder);
                }
                break;
        }
    }

    private static string AppendHistory(string currentJson, string status)
    {
        var list = string.IsNullOrWhiteSpace(currentJson) || currentJson == "[]"
            ? new List<StatusHistoryEntry>()
            : JsonSerializer.Deserialize<List<StatusHistoryEntry>>(currentJson, StatusHistoryJsonOptions) ?? new List<StatusHistoryEntry>();
        list.Add(new StatusHistoryEntry { Status = status, At = DateTime.UtcNow });
        return JsonSerializer.Serialize(list, StatusHistoryJsonOptions);
    }

    private sealed class StatusHistoryEntry
    {
        public string Status { get; set; } = string.Empty;
        public DateTime At { get; set; }
    }
}
