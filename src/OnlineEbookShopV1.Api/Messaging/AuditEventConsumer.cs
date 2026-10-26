using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OnlineEbookShopV1.Api.Messaging;

/// <summary>Background consumer that reads auth and payment events from the audit queue and passes each to the matching handlers.</summary>
public class AuditEventConsumer(
    IOptions<RabbitMqOptions> options,
    IEnumerable<IDomainEventHandler> handlers,
    ILogger<AuditEventConsumer> logger) : BackgroundService
{
    private readonly RabbitMqOptions _o = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_o.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { HostName = _o.Host, Port = _o.Port, UserName = _o.User, Password = _o.Password };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.ExchangeDeclareAsync(_o.Exchange, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(_o.AuditQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                await channel.QueueBindAsync(_o.AuditQueue, _o.Exchange, "#", cancellationToken: stoppingToken);

                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) => { closed.TrySetResult(); return Task.CompletedTask; };

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    await DispatchAsync(ea.Body, stoppingToken);
                    await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                };
                await channel.BasicConsumeAsync(_o.AuditQueue, autoAck: false, consumer, stoppingToken);
                logger.LogInformation("Listening for events on {Queue}", _o.AuditQueue);

                // Stay connected until shutdown, or reconnect if the broker drops us.
                await closed.Task.WaitAsync(stoppingToken);
                logger.LogWarning("RabbitMQ connection closed; reconnecting");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ consumer failed; retrying in 10s");
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>Runs every handler that accepts the event. A bad message or a failing handler is logged and does not stop the queue.</summary>
    public async Task DispatchAsync(ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        ReceivedEvent? evt;
        try
        {
            evt = ReceivedEvent.Parse(body.Span);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Discarding unreadable event message");
            return;
        }

        if (evt is null) return;
        foreach (var handler in handlers.Where(h => h.CanHandle(evt.EventType)))
        {
            try
            {
                await handler.HandleAsync(evt, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Handler} failed on {EventType}", handler.GetType().Name, evt.EventType);
            }
        }
    }
}
