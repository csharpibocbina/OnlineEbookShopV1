using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OnlineEbookShopV1.Api.Messaging;

/// <summary>Publishes domain events to a topic exchange. Failures are logged, never thrown, so the API stays available if the broker is down.</summary>
public sealed class RabbitMqEventPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _o = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(DomainEvent evt, CancellationToken ct = default)
    {
        if (!_o.Enabled) return;
        try
        {
            await _lock.WaitAsync(ct);
            try
            {
                var channel = await EnsureChannelAsync(ct);
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt, JsonSerializerOptions.Web));
                var props = new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent };
                await channel.BasicPublishAsync(_o.Exchange, evt.EventType, false, props, body, ct);
            }
            finally { _lock.Release(); }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish event {EventType} for {EntityId}", evt.EventType, evt.EntityId);
            await ResetAsync();
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        var factory = new ConnectionFactory { HostName = _o.Host, Port = _o.Port, UserName = _o.User, Password = _o.Password };
        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
        await _channel.ExchangeDeclareAsync(_o.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await _channel.QueueDeclareAsync(_o.AuditQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await _channel.QueueBindAsync(_o.AuditQueue, _o.Exchange, "#", cancellationToken: ct);
        return _channel;
    }

    private async Task ResetAsync()
    {
        try
        {
            if (_channel is not null) await _channel.DisposeAsync();
            if (_connection is not null) await _connection.DisposeAsync();
        }
        catch { /* best effort */ }
        _channel = null;
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _lock.Dispose();
    }
}
