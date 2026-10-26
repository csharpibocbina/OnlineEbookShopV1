using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Api.Payments;

public class PayPalPaymentGateway(HttpClient http, IOptions<PayPalOptions> options) : IPayPalGateway
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<PaymentRedirect> CreateOrderAsync(CheckoutSessionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ClientId) || string.IsNullOrWhiteSpace(options.Value.ClientSecret))
            throw AppException.ServiceUnavailable("PayPal payments are not configured.");

        var token = await GetAccessTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl}/v2/checkout/orders");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var body = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    custom_id = request.PaymentId.ToString(),
                    amount = new { currency_code = "USD", value = request.Amount.ToString("0.00") }
                }
            },
            application_context = new
            {
                return_url = request.SuccessUrl,
                cancel_url = request.CancelUrl,
                brand_name = "Online Ebook Shop"
            }
        };
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw AppException.BadRequest("PayPal checkout could not be created.");

        var order = JsonSerializer.Deserialize<PayPalOrderResponse>(json, JsonOpts)
            ?? throw AppException.BadRequest("PayPal returned an empty response.");
        var approve = order.Links?.FirstOrDefault(l => l.Rel == "approve")?.Href
            ?? throw AppException.BadRequest("PayPal did not return an approval URL.");
        return new PaymentRedirect(order.Id, approve);
    }

    // An approved PayPal order holds no money until it is captured. The buyer returns to
    // our success URL with ?token=<orderId>, and this call takes the payment.
    public async Task<ParsedWebhook> CaptureOrderAsync(string paypalOrderId, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl}/v2/checkout/orders/{Uri.EscapeDataString(paypalOrderId)}/capture");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            return new ParsedWebhook(paypalOrderId, PaymentStatuses.Failed, "capture.declined");
        if (!res.IsSuccessStatusCode)
            throw AppException.BadRequest("PayPal payment could not be captured.");

        var order = JsonSerializer.Deserialize<PayPalOrderResponse>(json, JsonOpts);
        var status = order?.Status switch
        {
            "COMPLETED" => PaymentStatuses.Succeeded,
            "VOIDED" => PaymentStatuses.Invalid,
            _ => PaymentStatuses.Pending
        };
        return new ParsedWebhook(paypalOrderId, status, "capture." + (order?.Status ?? "unknown").ToLowerInvariant());
    }

    public async Task<ParsedWebhook> ParseWebhookAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        if (!await VerifySignatureAsync(json, headers, ct))
            throw AppException.Unauthorized("Invalid PayPal webhook signature.");

        using var doc = JsonDocument.Parse(json);
        var eventType = doc.RootElement.GetProperty("event_type").GetString() ?? string.Empty;
        var resource = doc.RootElement.GetProperty("resource");
        var id = resource.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;

        // Capture events carry the capture id; the order id we stored is under related_ids.
        if (eventType.StartsWith("PAYMENT.CAPTURE.", StringComparison.Ordinal)
            && resource.TryGetProperty("supplementary_data", out var sup)
            && sup.TryGetProperty("related_ids", out var related)
            && related.TryGetProperty("order_id", out var orderIdEl))
        {
            id = orderIdEl.GetString() ?? id;
        }

        var status = eventType switch
        {
            "PAYMENT.CAPTURE.COMPLETED" => PaymentStatuses.Succeeded,
            "PAYMENT.CAPTURE.DENIED" or "PAYMENT.CAPTURE.DECLINED" => PaymentStatuses.Failed,
            "CHECKOUT.ORDER.VOIDED" => PaymentStatuses.Invalid,
            _ => PaymentStatuses.Pending
        };
        return new ParsedWebhook(id, status, eventType);
    }

    private async Task<bool> VerifySignatureAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.WebhookId))
            return false;

        string Header(string name) =>
            headers.TryGetValue(name, out var v) ? v : headers.TryGetValue(name.ToLowerInvariant(), out var v2) ? v2 : string.Empty;

        var token = await GetAccessTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl}/v1/notifications/verify-webhook-signature");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var payload = new
        {
            auth_algo = Header("PAYPAL-AUTH-ALGO"),
            cert_url = Header("PAYPAL-CERT-URL"),
            transmission_id = Header("PAYPAL-TRANSMISSION-ID"),
            transmission_sig = Header("PAYPAL-TRANSMISSION-SIG"),
            transmission_time = Header("PAYPAL-TRANSMISSION-TIME"),
            webhook_id = options.Value.WebhookId,
            webhook_event = JsonSerializer.Deserialize<JsonElement>(json)
        };
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) return false;
        var verify = JsonSerializer.Deserialize<PayPalVerifyResponse>(body, JsonOpts);
        return string.Equals(verify?.VerificationStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl}/v1/oauth2/token");
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.ClientId}:{options.Value.ClientSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw AppException.BadRequest("PayPal authentication failed.");
        var token = JsonSerializer.Deserialize<PayPalTokenResponse>(json, JsonOpts);
        return token?.AccessToken ?? throw AppException.BadRequest("PayPal authentication failed.");
    }

    private sealed class PayPalTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    }

    private sealed class PayPalVerifyResponse
    {
        [JsonPropertyName("verification_status")] public string VerificationStatus { get; set; } = string.Empty;
    }

    private sealed class PayPalOrderResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("links")] public List<PayPalLink>? Links { get; set; }
    }

    private sealed class PayPalLink
    {
        [JsonPropertyName("href")] public string Href { get; set; } = string.Empty;
        [JsonPropertyName("rel")] public string Rel { get; set; } = string.Empty;
    }
}
