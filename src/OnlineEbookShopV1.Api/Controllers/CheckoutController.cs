using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class CheckoutController(ICheckoutService checkout) : ControllerBase
{
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(CheckoutRequest request, CancellationToken ct) =>
        Ok(await checkout.CheckoutAsync(this.UserId(), request, ct));

    [HttpPost("checkout/paypal/capture")]
    public async Task<ActionResult<OrderResponse>> CapturePayPal(PayPalCaptureRequest request, CancellationToken ct) =>
        Ok(await checkout.CapturePayPalAsync(this.UserId(), request.PayPalOrderId, ct));

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> Orders() =>
        Ok(await checkout.ListOrdersAsync(this.UserId(), this.IsAdmin()));
}

[ApiController]
[Route("api/webhooks")]
public class WebhooksController(ICheckoutService checkout) : ControllerBase
{
    [HttpPost("stripe")]
    public async Task<IActionResult> Stripe(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();
        await checkout.HandleStripeWebhookAsync(json, signature, ct);
        return Ok();
    }

    [HttpPost("paypal")]
    public async Task<IActionResult> PayPal(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        await checkout.HandlePayPalWebhookAsync(json, headers, ct);
        return Ok();
    }
}

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
