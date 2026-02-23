using System.Text;
using System.Text.Json;
using Logistics.Application.Commands;
using Logistics.Application.DispatchR;
using Logistics.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Logistics.WriteService;

public static class CommandTypes
{
    public const string PlaceOrder = "PlaceOrder";
    public const string PackOrder = "PackOrder";
    public const string ChangeAddress = "ChangeAddress";
    public const string FailDelivery = "FailDelivery";
}

public sealed class RabbitMqCommandConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqCommandConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly ResiliencePipeline ConcurrencyRetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<ConcurrencyException>(),
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        })
        .Build();

    public RabbitMqCommandConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqCommandConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        const int maxRetries = 30;
        const int delayMs = 2000;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "RabbitMQ connection attempt {Attempt}/{Max} failed. Retrying in {Delay}ms.", attempt, maxRetries, delayMs);
                await Task.Delay(delayMs, stoppingToken);
            }
        }

        if (_connection == null || _channel == null)
            throw new InvalidOperationException("Could not connect to RabbitMQ after retries.");

        await _channel.QueueDeclareAsync(_options.CommandQueue, true, false, false, null);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) => await OnCommandReceivedAsync(ea);

        await _channel.BasicConsumeAsync(_options.CommandQueue, autoAck: false, consumer, stoppingToken);

        _logger.LogInformation("RabbitMQ command consumer started (single queue: {Queue}).", _options.CommandQueue);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnCommandReceivedAsync(BasicDeliverEventArgs ea)
    {
        try
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            var envelope = JsonSerializer.Deserialize<CommandEnvelope>(body, JsonOptions);
            if (envelope is null || string.IsNullOrEmpty(envelope.CommandType))
            {
                _logger.LogWarning("Invalid command envelope (missing CommandType), nacking.");
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, false);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dispatchR = scope.ServiceProvider.GetRequiredService<IDispatchR>();

            switch (envelope.CommandType)
            {
                case CommandTypes.PlaceOrder:
                    var placeCmd = envelope.Payload.Deserialize<PlaceOrderCommand>(JsonOptions);
                    if (placeCmd is null) { await NackInvalidPayloadAsync(ea, "PlaceOrder"); return; }
                    await ConcurrencyRetryPipeline.ExecuteAsync(ct => new ValueTask(dispatchR.SendAsync(placeCmd, ct)), CancellationToken.None);
                    break;
                case CommandTypes.PackOrder:
                    var packCmd = envelope.Payload.Deserialize<PackOrderCommand>(JsonOptions);
                    if (packCmd is null) { await NackInvalidPayloadAsync(ea, "PackOrder"); return; }
                    await ConcurrencyRetryPipeline.ExecuteAsync(ct => new ValueTask(dispatchR.SendAsync(packCmd, ct)), CancellationToken.None);
                    break;
                case CommandTypes.ChangeAddress:
                    var changeCmd = envelope.Payload.Deserialize<ChangeAddressCommand>(JsonOptions);
                    if (changeCmd is null) { await NackInvalidPayloadAsync(ea, "ChangeAddress"); return; }
                    await ConcurrencyRetryPipeline.ExecuteAsync(ct => new ValueTask(dispatchR.SendAsync(changeCmd, ct)), CancellationToken.None);
                    break;
                case CommandTypes.FailDelivery:
                    var failCmd = envelope.Payload.Deserialize<FailDeliveryCommand>(JsonOptions);
                    if (failCmd is null) { await NackInvalidPayloadAsync(ea, "FailDelivery"); return; }
                    await ConcurrencyRetryPipeline.ExecuteAsync(ct => new ValueTask(dispatchR.SendAsync(failCmd, ct)), CancellationToken.None);
                    break;
                default:
                    _logger.LogWarning("Unknown command type '{CommandType}', nacking.", envelope.CommandType);
                    await _channel!.BasicNackAsync(ea.DeliveryTag, false, false);
                    return;
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command from queue.");
            await _channel!.BasicNackAsync(ea.DeliveryTag, false, true);
        }
    }

    private async Task NackInvalidPayloadAsync(BasicDeliverEventArgs ea, string commandType)
    {
        _logger.LogWarning("Invalid {CommandType} payload, nacking.", commandType);
        await _channel!.BasicNackAsync(ea.DeliveryTag, false, false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
