using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Mvc.Api;

namespace OnlineEbookShopV1.Mvc.Controllers;

public class CheckoutController(ShopApiClient api) : Controller
{
    public IActionResult Index()
    {
        if (!AuthCookieStore.IsSignedIn(Request))
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index") });
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string provider, CancellationToken ct)
    {
        if (!AuthCookieStore.IsSignedIn(Request))
            return RedirectToAction("Login", "Account");
        var success = Url.Action("Success", "Checkout", null, Request.Scheme)!;
        var cancel = Url.Action("Cancel", "Checkout", null, Request.Scheme)!;
        try
        {
            var result = await api.CheckoutAsync(provider, success, cancel, ct);
            return Redirect(result.RedirectUrl);
        }
        catch (ShopApiException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Cart");
        }
    }

    // PayPal sends the buyer back with ?token=<PayPal order id>; the payment is only taken once we capture it.
    // Stripe needs no call here because its webhook confirms the payment.
    public async Task<IActionResult> Success(string? token, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(token) && AuthCookieStore.IsSignedIn(Request))
        {
            try
            {
                var order = await api.CapturePayPalAsync(token, ct);
                if (order.Status != "Paid")
                {
                    TempData["Error"] = "PayPal did not complete the payment.";
                    return RedirectToAction("Orders");
                }
            }
            catch (ShopApiException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Orders");
            }
        }

        return View();
    }

    public IActionResult Cancel() => View();

    public async Task<IActionResult> Orders(CancellationToken ct)
    {
        if (!AuthCookieStore.IsSignedIn(Request))
            return RedirectToAction("Login", "Account");
        return View(await api.ListOrdersAsync(ct));
    }
}
