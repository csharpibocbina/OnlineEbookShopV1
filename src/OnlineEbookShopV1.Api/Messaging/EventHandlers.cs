using System.Text.Json;

namespace OnlineEbookShopV1.Api.Messaging;

/// <summary>Handles domain events delivered by <see cref="AuditEventConsumer"/>. Register more implementations to react to events.</summary>
public interface IDomainEventHandler
{
    bool CanHandle(string eventType);
    Task HandleAsync(ReceivedEvent evt, CancellationToken ct);
}

/// <summary>A domain event as read back off the queue; the payload stays raw JSON.</summary>
public record ReceivedEvent(string EventType, string EntityType, Guid EntityId, Guid? ActorId, DateTime OccurredAt, JsonElement? Payload)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ReceivedEvent? Parse(ReadOnlySpan<byte> body) => JsonSerializer.Deserialize<ReceivedEvent>(body, Json);

    public string? PayloadValue(string name) =>
        Payload is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()
            : null;
}

public class AuthEventHandler(ILogger<AuthEventHandler> logger) : IDomainEventHandler
{
    public bool CanHandle(string eventType) => eventType.StartsWith("auth.", StringComparison.Ordinal);

    public Task HandleAsync(ReceivedEvent evt, CancellationToken ct)
    {
        switch (evt.EventType)
        {
            case EventTypes.LoginSucceeded:
                logger.LogInformation("User {UserId} ({Email}) logged in", evt.EntityId, evt.PayloadValue("email"));
                break;
            case EventTypes.LoginFailed:
                logger.LogWarning("Failed login attempt for {Email}", evt.PayloadValue("email"));
                break;
            case EventTypes.Logout:
                logger.LogInformation("User {UserId} logged out", evt.EntityId);
                break;
            case EventTypes.InvalidAccount:
                logger.LogWarning("Inactive or missing account {UserId} tried to use the shop", evt.EntityId);
                break;
            default:
                logger.LogInformation("Unhandled auth event {EventType}", evt.EventType);
                break;
        }

        return Task.CompletedTask;
    }
}

public class PaymentEventHandler(ILogger<PaymentEventHandler> logger) : IDomainEventHandler
{
    public bool CanHandle(string eventType) => eventType.StartsWith("payment.", StringComparison.Ordinal);

    public Task HandleAsync(ReceivedEvent evt, CancellationToken ct)
    {
        var provider = evt.PayloadValue("provider");
        var amount = evt.PayloadValue("amount");
        switch (evt.EventType)
        {
            case EventTypes.PaymentSucceeded:
                logger.LogInformation("Payment {PaymentId} of {Amount} via {Provider} succeeded for user {UserId}", evt.EntityId, amount, provider, evt.ActorId);
                break;
            case EventTypes.PaymentPending:
                logger.LogInformation("Payment {PaymentId} of {Amount} via {Provider} is pending for user {UserId}", evt.EntityId, amount, provider, evt.ActorId);
                break;
            case EventTypes.PaymentInvalid:
                logger.LogWarning("Payment {PaymentId} via {Provider} is invalid or failed: {Payload}", evt.EntityId, provider, evt.Payload?.ToString());
                break;
            default:
                logger.LogInformation("Unhandled payment event {EventType}", evt.EventType);
                break;
        }

        return Task.CompletedTask;
    }
}
