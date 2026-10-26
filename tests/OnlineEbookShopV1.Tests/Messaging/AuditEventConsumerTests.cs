using System.Text;
using System.Text.Json;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OnlineEbookShopV1.Api.Messaging;

namespace OnlineEbookShopV1.Tests.Messaging;

public class AuditEventConsumerTests
{
    private static ReadOnlyMemory<byte> Serialize(DomainEvent evt) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt, JsonSerializerOptions.Web));

    private static AuditEventConsumer CreateSut(params IDomainEventHandler[] handlers) =>
        new(Options.Create(new RabbitMqOptions()), handlers, NullLogger<AuditEventConsumer>.Instance);

    private static IDomainEventHandler Handler(string prefix)
    {
        var handler = A.Fake<IDomainEventHandler>();
        A.CallTo(() => handler.CanHandle(A<string>._)).ReturnsLazily((string t) => t.StartsWith(prefix));
        return handler;
    }

    [Theory]
    [InlineData(EventTypes.LoginSucceeded)]
    [InlineData(EventTypes.LoginFailed)]
    [InlineData(EventTypes.Logout)]
    [InlineData(EventTypes.InvalidAccount)]
    public async Task Auth_events_go_to_auth_handlers_only(string eventType)
    {
        var auth = Handler("auth.");
        var payment = Handler("payment.");
        var userId = Guid.NewGuid();

        await CreateSut(auth, payment).DispatchAsync(Serialize(DomainEvents.Create(eventType, "User", userId, userId, new { email = "jay@test.local" })), default);

        A.CallTo(() => auth.HandleAsync(A<ReceivedEvent>.That.Matches(e => e.EventType == eventType && e.EntityId == userId && e.PayloadValue("email") == "jay@test.local"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => payment.HandleAsync(A<ReceivedEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(EventTypes.PaymentSucceeded)]
    [InlineData(EventTypes.PaymentPending)]
    [InlineData(EventTypes.PaymentInvalid)]
    public async Task Payment_events_go_to_payment_handlers_with_payload(string eventType)
    {
        var payment = Handler("payment.");

        await CreateSut(payment).DispatchAsync(Serialize(DomainEvents.Create(eventType, "Payment", Guid.NewGuid(), null, new { Provider = "Stripe", Amount = 12.5m })), default);

        A.CallTo(() => payment.HandleAsync(A<ReceivedEvent>.That.Matches(e => e.PayloadValue("provider") == "Stripe" && e.PayloadValue("amount") == "12.5"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task A_failing_handler_does_not_stop_the_others()
    {
        var broken = Handler("auth.");
        A.CallTo(() => broken.HandleAsync(A<ReceivedEvent>._, A<CancellationToken>._)).Throws(new InvalidOperationException("boom"));
        var healthy = Handler("auth.");

        await CreateSut(broken, healthy).DispatchAsync(Serialize(DomainEvents.Create(EventTypes.Logout, "User", Guid.NewGuid(), null)), default);

        A.CallTo(() => healthy.HandleAsync(A<ReceivedEvent>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Unreadable_message_is_discarded_without_throwing()
    {
        var handler = Handler("");

        await CreateSut(handler).DispatchAsync(Encoding.UTF8.GetBytes("not json"), default);

        A.CallTo(() => handler.HandleAsync(A<ReceivedEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Built_in_handlers_accept_their_event_families()
    {
        var auth = new AuthEventHandler(NullLogger<AuthEventHandler>.Instance);
        var payment = new PaymentEventHandler(NullLogger<PaymentEventHandler>.Instance);

        Assert.True(auth.CanHandle(EventTypes.LoginFailed));
        Assert.False(auth.CanHandle(EventTypes.PaymentSucceeded));
        Assert.True(payment.CanHandle(EventTypes.PaymentInvalid));
        Assert.False(payment.CanHandle(EventTypes.Logout));

        var evt = new ReceivedEvent(EventTypes.PaymentSucceeded, "Payment", Guid.NewGuid(), null, DateTime.UtcNow, null);
        await payment.HandleAsync(evt, default);
        await auth.HandleAsync(evt with { EventType = EventTypes.LoginSucceeded }, default);
    }
}
