using Microsoft.Extensions.Options;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Services;
using Stripe;
using Stripe.Checkout;

namespace OnlineEbookShopV1.Api.Payments;

public class StripePaymentGateway(IOptions<StripeOptions> options) : IStripeGateway
{
    public async Task<PaymentRedirect> CreateSessionAsync(CheckoutSessionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SecretKey))
            throw AppException.ServiceUnavailable("Stripe payments are not configured.");

        var service = new SessionService(new StripeClient(options.Value.SecretKey));
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = request.OrderId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = request.OrderId.ToString(),
                ["paymentId"] = request.PaymentId.ToString()
            },
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string> { ["paymentId"] = request.PaymentId.ToString() }
            },
            LineItems = request.Lines.Select(i => new SessionLineItemOptions
            {
                Quantity = i.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "usd",
                    UnitAmount = (long)Math.Round(i.UnitPrice * 100m, MidpointRounding.AwayFromZero),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = i.Title }
                }
            }).ToList()
        }, cancellationToken: ct);

        return new PaymentRedirect(session.Id, session.Url ?? throw new InvalidOperationException("Stripe did not return a checkout URL."));
    }

    public ParsedWebhook ParseWebhook(string json, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            throw AppException.BadRequest("Missing Stripe-Signature header.");

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, options.Value.WebhookSecret);
        }
        catch (StripeException)
        {
            throw AppException.BadRequest("Invalid Stripe webhook signature.");
        }

        return stripeEvent.Type switch
        {
            EventTypes.CheckoutSessionCompleted when stripeEvent.Data.Object is Session completed =>
                new ParsedWebhook(completed.Id, PaymentStatuses.Succeeded, stripeEvent.Type),
            EventTypes.CheckoutSessionExpired when stripeEvent.Data.Object is Session expired =>
                new ParsedWebhook(expired.Id, PaymentStatuses.Invalid, stripeEvent.Type),
            EventTypes.PaymentIntentPaymentFailed when stripeEvent.Data.Object is PaymentIntent intent =>
                new ParsedWebhook(intent.Id, PaymentStatuses.Failed, stripeEvent.Type, PaymentIdFrom(intent.Metadata)),
            _ => new ParsedWebhook(stripeEvent.Id, PaymentStatuses.Pending, stripeEvent.Type)
        };
    }

    private static Guid? PaymentIdFrom(IDictionary<string, string>? metadata) =>
        metadata is not null && metadata.TryGetValue("paymentId", out var raw) && Guid.TryParse(raw, out var id) ? id : null;
}
