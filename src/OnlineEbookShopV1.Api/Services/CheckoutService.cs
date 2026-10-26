using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Data;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Messaging;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Payments;

namespace OnlineEbookShopV1.Api.Services;

public interface ICheckoutService
{
    Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken ct = default);
    Task HandleStripeWebhookAsync(string json, string? signature, CancellationToken ct = default);
    Task HandlePayPalWebhookAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);
    Task ApplyPaymentResultAsync(string externalId, string status, CancellationToken ct = default);
    Task<OrderResponse> CapturePayPalAsync(Guid userId, string paypalOrderId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderResponse>> ListOrdersAsync(Guid userId, bool allUsers);
}

public class CheckoutService(
    AppDbContext db,
    IEventPublisher events,
    IStripeGateway stripe,
    IPayPalGateway paypal) : ICheckoutService
{
    public async Task<CheckoutResponse> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken ct = default)
    {
        var provider = NormalizeProvider(request.Provider);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
        {
            await events.PublishAsync(DomainEvents.Create(EventTypes.InvalidAccount, "User", userId, userId, null), ct);
            throw AppException.Unauthorized("Invalid user account.");
        }

        var cart = await db.Carts.Include(c => c.Items).ThenInclude(i => i.Ebook)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cart is null || cart.Items.Count == 0)
            throw AppException.BadRequest("Cart is empty.");

        var lines = cart.Items.Where(i => i.Ebook is { IsPublished: true }).ToList();
        if (lines.Count == 0)
            throw AppException.BadRequest("Cart is empty.");

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatuses.Pending,
            Total = lines.Sum(i => i.Ebook!.Price * i.Quantity),
            Items = lines.Select(i => new OrderItem
            {
                EbookId = i.EbookId,
                Title = i.Ebook!.Title,
                UnitPrice = i.Ebook.Price,
                Quantity = i.Quantity
            }).ToList()
        };
        var payment = new Payment
        {
            Provider = provider,
            Status = PaymentStatuses.Pending,
            Amount = order.Total
        };
        order.Payments.Add(payment);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        await events.PublishAsync(DomainEvents.Create(EventTypes.PaymentPending, "Payment", payment.Id, userId, new { payment.Provider, payment.Amount }), ct);

        var session = new CheckoutSessionRequest(
            order.Id,
            payment.Id,
            order.Total,
            lines.Select(i => new CheckoutLine(i.Ebook!.Title, i.Ebook.Price, i.Quantity)).ToList(),
            request.SuccessUrl,
            request.CancelUrl);

        PaymentRedirect redirect;
        try
        {
            redirect = provider == PaymentProviders.Stripe
                ? await stripe.CreateSessionAsync(session, ct)
                : await paypal.CreateOrderAsync(session, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The provider never accepted the payment, so close it out instead of leaving it pending.
            payment.Status = PaymentStatuses.Invalid;
            payment.CompletedAt = DateTime.UtcNow;
            order.Status = OrderStatuses.Failed;
            await db.SaveChangesAsync(CancellationToken.None);
            await events.PublishAsync(DomainEvents.Create(EventTypes.PaymentInvalid, "Payment", payment.Id, userId, new { payment.Provider, reason = ex.Message }), CancellationToken.None);
            throw;
        }

        payment.ExternalId = redirect.ExternalId;
        await db.SaveChangesAsync(ct);
        return new CheckoutResponse(order.Id, payment.Id, redirect.RedirectUrl, provider);
    }

    public async Task HandleStripeWebhookAsync(string json, string? signature, CancellationToken ct = default)
    {
        var parsed = stripe.ParseWebhook(json, signature);
        await ApplyParsedAsync(parsed, ct);
    }

    public async Task HandlePayPalWebhookAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        var parsed = await paypal.ParseWebhookAsync(json, headers, ct);
        await ApplyParsedAsync(parsed, ct);
    }

    public Task ApplyPaymentResultAsync(string externalId, string status, CancellationToken ct = default) =>
        ApplyParsedAsync(new ParsedWebhook(externalId, status, "manual"), ct);

    public async Task<OrderResponse> CapturePayPalAsync(Guid userId, string paypalOrderId, CancellationToken ct = default)
    {
        var payment = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ExternalId == paypalOrderId && p.Provider == PaymentProviders.PayPal && p.Order!.UserId == userId, ct)
            ?? throw AppException.NotFound("Payment not found.");

        if (payment.Status == PaymentStatuses.Pending)
            await ApplyParsedAsync(await paypal.CaptureOrderAsync(paypalOrderId, ct), ct);

        var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Payments)
            .FirstAsync(o => o.Id == payment.OrderId, ct);
        return ToResponse(order);
    }

    public async Task<IReadOnlyList<OrderResponse>> ListOrdersAsync(Guid userId, bool allUsers)
    {
        var q = db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Payments).AsQueryable();
        if (!allUsers) q = q.Where(o => o.UserId == userId);
        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return orders.Select(ToResponse).ToList();
    }

    private async Task ApplyParsedAsync(ParsedWebhook parsed, CancellationToken ct)
    {
        if (parsed.Status == PaymentStatuses.Pending)
            return;

        var payment = await db.Payments.Include(p => p.Order).ThenInclude(o => o!.Items)
            .FirstOrDefaultAsync(p => p.ExternalId == parsed.ExternalId || (parsed.PaymentId != null && p.Id == parsed.PaymentId), ct);

        if (payment is null)
        {
            await events.PublishAsync(DomainEvents.Create(EventTypes.PaymentInvalid, "Payment", Guid.Empty, null, new { parsed.ExternalId, parsed.EventName }), ct);
            return;
        }

        // A buyer can retry a declined card in the same Stripe session, so a later success
        // may follow a failure. Nothing changes a succeeded payment, and failures are recorded once.
        if (payment.Status == PaymentStatuses.Succeeded
            || (parsed.Status != PaymentStatuses.Succeeded && payment.Status != PaymentStatuses.Pending))
            return;

        if (parsed.Status == PaymentStatuses.Succeeded)
        {
            payment.Status = PaymentStatuses.Succeeded;
            payment.CompletedAt = DateTime.UtcNow;
            payment.Order!.Status = OrderStatuses.Paid;
            var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == payment.Order.UserId, ct);
            if (cart is not null)
            {
                db.CartItems.RemoveRange(cart.Items);
                cart.Items.Clear();
            }

            await db.SaveChangesAsync(ct);
            await events.PublishAsync(DomainEvents.Create(EventTypes.PaymentSucceeded, "Payment", payment.Id, payment.Order.UserId, new { payment.Provider, payment.Amount }), ct);
            return;
        }

        payment.Status = parsed.Status == PaymentStatuses.Failed ? PaymentStatuses.Failed : PaymentStatuses.Invalid;
        payment.CompletedAt = DateTime.UtcNow;
        payment.Order!.Status = OrderStatuses.Failed;
        await db.SaveChangesAsync(ct);
        await events.PublishAsync(DomainEvents.Create(EventTypes.PaymentInvalid, "Payment", payment.Id, payment.Order.UserId, new { payment.Provider, payment.Status }), ct);
    }

    private static string NormalizeProvider(string provider) =>
        provider.Equals(PaymentProviders.Stripe, StringComparison.OrdinalIgnoreCase) ? PaymentProviders.Stripe
        : provider.Equals(PaymentProviders.PayPal, StringComparison.OrdinalIgnoreCase) ? PaymentProviders.PayPal
        : throw AppException.BadRequest("Provider must be Stripe or PayPal.");

    private static OrderResponse ToResponse(Order o) => new(
        o.Id,
        o.Status,
        o.Total,
        o.CreatedAt,
        o.Items.Select(i => new OrderItemResponse(i.EbookId, i.Title, i.UnitPrice, i.Quantity)).ToList(),
        o.Payments.Select(p => new PaymentResponse(p.Id, p.Provider, p.Status, p.ExternalId, p.Amount)).ToList());
}
