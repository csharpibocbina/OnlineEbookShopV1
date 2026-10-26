using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Data;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Messaging;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Payments;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Tests.Services;

public class CheckoutServiceTests
{
    private readonly IEventPublisher _events = A.Fake<IEventPublisher>();
    private readonly IStripeGateway _stripe = A.Fake<IStripeGateway>();
    private readonly IPayPalGateway _paypal = A.Fake<IPayPalGateway>();

    private CheckoutService CreateSut(AppDbContext db) => new(db, _events, _stripe, _paypal);

    private static CheckoutRequest Request(string provider) => new(provider, "https://shop/success", "https://shop/cancel");

    private void AssertPublished(string eventType, int times = 1) =>
        A.CallTo(() => _events.PublishAsync(A<DomainEvent>.That.Matches(e => e.EventType == eventType), A<CancellationToken>._))
            .MustHaveHappened(times, Times.Exactly);

    private async Task<AppUser> UserWithCartAsync(AppDbContext db, decimal price = 10m, int quantity = 2)
    {
        var user = await db.AddUserAsync();
        var ebook = await db.AddEbookAsync(price: price);
        await new CartService(db, _events).AddAsync(user.Id, new CartItemRequest(ebook.Id, quantity));
        return user;
    }

    [Fact]
    public async Task Stripe_checkout_creates_pending_order_and_returns_redirect()
    {
        await using var db = TestDb.Create();
        var user = await UserWithCartAsync(db, 10m, 2);
        A.CallTo(() => _stripe.CreateSessionAsync(A<CheckoutSessionRequest>._, A<CancellationToken>._))
            .Returns(new PaymentRedirect("cs_test_1", "https://checkout.stripe.com/cs_test_1"));

        var result = await CreateSut(db).CheckoutAsync(user.Id, Request("stripe"));

        Assert.Equal(PaymentProviders.Stripe, result.Provider);
        Assert.Equal("https://checkout.stripe.com/cs_test_1", result.RedirectUrl);
        var order = await db.Orders.Include(o => o.Payments).Include(o => o.Items).SingleAsync();
        Assert.Equal(OrderStatuses.Pending, order.Status);
        Assert.Equal(20m, order.Total);
        Assert.Equal("cs_test_1", Assert.Single(order.Payments).ExternalId);
        A.CallTo(() => _stripe.CreateSessionAsync(A<CheckoutSessionRequest>.That.Matches(r => r.Amount == 20m && r.SuccessUrl == "https://shop/success"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _paypal.CreateOrderAsync(A<CheckoutSessionRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertPublished(EventTypes.PaymentPending);
    }

    [Fact]
    public async Task PayPal_checkout_uses_paypal_gateway()
    {
        await using var db = TestDb.Create();
        var user = await UserWithCartAsync(db);
        A.CallTo(() => _paypal.CreateOrderAsync(A<CheckoutSessionRequest>._, A<CancellationToken>._))
            .Returns(new PaymentRedirect("PP-ORDER-1", "https://paypal/approve"));

        var result = await CreateSut(db).CheckoutAsync(user.Id, Request("PayPal"));

        Assert.Equal(PaymentProviders.PayPal, result.Provider);
        A.CallTo(() => _stripe.CreateSessionAsync(A<CheckoutSessionRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Unknown_provider_is_rejected()
    {
        await using var db = TestDb.Create();
        var user = await UserWithCartAsync(db);

        var ex = await Assert.ThrowsAsync<AppException>(() => CreateSut(db).CheckoutAsync(user.Id, Request("Bitcoin")));

        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task Empty_cart_is_rejected()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync();

        var ex = await Assert.ThrowsAsync<AppException>(() => CreateSut(db).CheckoutAsync(user.Id, Request("Stripe")));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_checkout_publishes_invalid_account()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync(isActive: false);

        await Assert.ThrowsAsync<AppException>(() => CreateSut(db).CheckoutAsync(user.Id, Request("Stripe")));

        AssertPublished(EventTypes.InvalidAccount);
    }

    [Fact]
    public async Task Gateway_failure_marks_payment_invalid_and_order_failed()
    {
        await using var db = TestDb.Create();
        var user = await UserWithCartAsync(db);
        A.CallTo(() => _stripe.CreateSessionAsync(A<CheckoutSessionRequest>._, A<CancellationToken>._))
            .Throws(AppException.ServiceUnavailable("Stripe payments are not configured."));

        var ex = await Assert.ThrowsAsync<AppException>(() => CreateSut(db).CheckoutAsync(user.Id, Request("Stripe")));

        Assert.Equal(503, ex.StatusCode);
        var order = await db.Orders.Include(o => o.Payments).SingleAsync();
        Assert.Equal(OrderStatuses.Failed, order.Status);
        Assert.Equal(PaymentStatuses.Invalid, Assert.Single(order.Payments).Status);
        AssertPublished(EventTypes.PaymentPending);
        AssertPublished(EventTypes.PaymentInvalid);
    }

    private async Task<(AppUser User, CheckoutService Sut)> PendingStripeOrderAsync(AppDbContext db)
    {
        var user = await UserWithCartAsync(db);
        A.CallTo(() => _stripe.CreateSessionAsync(A<CheckoutSessionRequest>._, A<CancellationToken>._))
            .Returns(new PaymentRedirect("cs_1", "https://checkout.stripe.com/cs_1"));
        var sut = CreateSut(db);
        await sut.CheckoutAsync(user.Id, Request("Stripe"));
        return (user, sut);
    }

    [Fact]
    public async Task Successful_webhook_marks_order_paid_clears_cart_and_publishes_once()
    {
        await using var db = TestDb.Create();
        var (_, sut) = await PendingStripeOrderAsync(db);
        A.CallTo(() => _stripe.ParseWebhook("{}", "sig"))
            .Returns(new ParsedWebhook("cs_1", PaymentStatuses.Succeeded, "checkout.session.completed"));

        await sut.HandleStripeWebhookAsync("{}", "sig");
        await sut.HandleStripeWebhookAsync("{}", "sig");

        var order = await db.Orders.Include(o => o.Payments).SingleAsync();
        Assert.Equal(OrderStatuses.Paid, order.Status);
        Assert.Equal(PaymentStatuses.Succeeded, order.Payments[0].Status);
        Assert.Empty(db.CartItems);
        AssertPublished(EventTypes.PaymentSucceeded);
    }

    [Fact]
    public async Task Failed_webhook_marks_order_failed_and_a_later_success_still_wins()
    {
        await using var db = TestDb.Create();
        var (_, sut) = await PendingStripeOrderAsync(db);

        await sut.ApplyPaymentResultAsync("cs_1", PaymentStatuses.Failed);
        Assert.Equal(OrderStatuses.Failed, (await db.Orders.SingleAsync()).Status);
        AssertPublished(EventTypes.PaymentInvalid);

        await sut.ApplyPaymentResultAsync("cs_1", PaymentStatuses.Succeeded);
        Assert.Equal(OrderStatuses.Paid, (await db.Orders.SingleAsync()).Status);
        AssertPublished(EventTypes.PaymentSucceeded);
    }

    [Fact]
    public async Task Pending_webhook_changes_nothing()
    {
        await using var db = TestDb.Create();
        var (_, sut) = await PendingStripeOrderAsync(db);

        await sut.ApplyPaymentResultAsync("cs_1", PaymentStatuses.Pending);

        Assert.Equal(OrderStatuses.Pending, (await db.Orders.SingleAsync()).Status);
        AssertPublished(EventTypes.PaymentSucceeded, 0);
    }

    [Fact]
    public async Task Webhook_for_unknown_payment_publishes_payment_invalid()
    {
        await using var db = TestDb.Create();

        await CreateSut(db).ApplyPaymentResultAsync("cs_unknown", PaymentStatuses.Succeeded);

        AssertPublished(EventTypes.PaymentInvalid);
    }

    [Fact]
    public async Task Webhook_can_match_payment_by_our_payment_id()
    {
        await using var db = TestDb.Create();
        var (_, sut) = await PendingStripeOrderAsync(db);
        var paymentId = (await db.Payments.SingleAsync()).Id;
        A.CallTo(() => _stripe.ParseWebhook(A<string>._, A<string>._))
            .Returns(new ParsedWebhook("pi_other", PaymentStatuses.Failed, "payment_intent.payment_failed", paymentId));

        await sut.HandleStripeWebhookAsync("{}", "sig");

        Assert.Equal(PaymentStatuses.Failed, (await db.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task PayPal_capture_marks_order_paid()
    {
        await using var db = TestDb.Create();
        var user = await UserWithCartAsync(db);
        A.CallTo(() => _paypal.CreateOrderAsync(A<CheckoutSessionRequest>._, A<CancellationToken>._))
            .Returns(new PaymentRedirect("PP-1", "https://paypal/approve"));
        A.CallTo(() => _paypal.CaptureOrderAsync("PP-1", A<CancellationToken>._))
            .Returns(new ParsedWebhook("PP-1", PaymentStatuses.Succeeded, "capture.completed"));
        var sut = CreateSut(db);
        await sut.CheckoutAsync(user.Id, Request("PayPal"));

        var order = await sut.CapturePayPalAsync(user.Id, "PP-1");

        Assert.Equal(OrderStatuses.Paid, order.Status);
        AssertPublished(EventTypes.PaymentSucceeded);
    }

    [Fact]
    public async Task PayPal_capture_of_another_users_order_is_not_found()
    {
        await using var db = TestDb.Create();
        var owner = await UserWithCartAsync(db);
        var other = await db.AddUserAsync("other@test.local");
        A.CallTo(() => _paypal.CreateOrderAsync(A<CheckoutSessionRequest>._, A<CancellationToken>._))
            .Returns(new PaymentRedirect("PP-1", "https://paypal/approve"));
        var sut = CreateSut(db);
        await sut.CheckoutAsync(owner.Id, Request("PayPal"));

        var ex = await Assert.ThrowsAsync<AppException>(() => sut.CapturePayPalAsync(other.Id, "PP-1"));

        Assert.Equal(404, ex.StatusCode);
        A.CallTo(() => _paypal.CaptureOrderAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Orders_are_scoped_to_user_unless_admin()
    {
        await using var db = TestDb.Create();
        var (user, sut) = await PendingStripeOrderAsync(db);
        var someoneElse = Guid.NewGuid();

        Assert.Single(await sut.ListOrdersAsync(user.Id, allUsers: false));
        Assert.Empty(await sut.ListOrdersAsync(someoneElse, allUsers: false));
        Assert.Single(await sut.ListOrdersAsync(someoneElse, allUsers: true));
    }
}
