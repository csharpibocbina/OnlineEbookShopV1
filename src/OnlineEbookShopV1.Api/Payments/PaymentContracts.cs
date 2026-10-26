namespace OnlineEbookShopV1.Api.Payments;

public record CheckoutLine(string Title, decimal UnitPrice, int Quantity);

public record CheckoutSessionRequest(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    IReadOnlyList<CheckoutLine> Lines,
    string SuccessUrl,
    string CancelUrl);

public record PaymentRedirect(string ExternalId, string RedirectUrl);

// PaymentId is set when the provider echoes back our own payment id (e.g. Stripe metadata),
// which lets events that carry a different external id still find the payment.
public record ParsedWebhook(string ExternalId, string Status, string EventName, Guid? PaymentId = null);

public interface IStripeGateway
{
    Task<PaymentRedirect> CreateSessionAsync(CheckoutSessionRequest request, CancellationToken ct = default);
    ParsedWebhook ParseWebhook(string json, string? signature);
}

public interface IPayPalGateway
{
    Task<PaymentRedirect> CreateOrderAsync(CheckoutSessionRequest request, CancellationToken ct = default);
    Task<ParsedWebhook> CaptureOrderAsync(string paypalOrderId, CancellationToken ct = default);
    Task<ParsedWebhook> ParseWebhookAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);
}

public class StripeOptions
{
    public const string Section = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

public class PayPalOptions
{
    public const string Section = "PayPal";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Mode { get; set; } = "sandbox";
    public string WebhookId { get; set; } = string.Empty;

    public string BaseUrl => Mode.Equals("live", StringComparison.OrdinalIgnoreCase)
        ? "https://api-m.paypal.com"
        : "https://api-m.sandbox.paypal.com";
}
