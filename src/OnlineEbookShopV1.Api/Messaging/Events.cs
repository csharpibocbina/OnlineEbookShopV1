namespace OnlineEbookShopV1.Api.Messaging;

public record DomainEvent(string EventType, string EntityType, Guid EntityId, Guid? ActorId, DateTime OccurredAt, object? Payload);

public static class EventTypes
{
    public const string LoginSucceeded = "auth.login.succeeded";
    public const string LoginFailed = "auth.login.failed";
    public const string Logout = "auth.logout";
    public const string InvalidAccount = "auth.invalid_account";
    public const string PaymentSucceeded = "payment.succeeded";
    public const string PaymentPending = "payment.pending";
    public const string PaymentInvalid = "payment.invalid";
}

public interface IEventPublisher
{
    Task PublishAsync(DomainEvent evt, CancellationToken ct = default);
}

public static class DomainEvents
{
    public static DomainEvent Create(string type, string entityType, Guid entityId, Guid? actorId, object? payload = null) =>
        new(type, entityType, entityId, actorId, DateTime.UtcNow, payload);
}
