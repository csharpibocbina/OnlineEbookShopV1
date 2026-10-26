using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Mvc.Api;

namespace OnlineEbookShopV1.Mvc.Controllers;

public class CartController(ShopApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!AuthCookieStore.IsSignedIn(Request))
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index") });
        try
        {
            return View(await api.GetCartAsync(ct));
        }
        catch (ShopApiException ex) when (ex.StatusCode == 401)
        {
            return RedirectToAction("Login", "Account");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(Guid ebookId, int quantity = 1, CancellationToken ct = default)
    {
        if (!AuthCookieStore.IsSignedIn(Request))
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Catalog", new { id = ebookId }) });
        await api.AddToCartAsync(ebookId, quantity, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid ebookId, int quantity, CancellationToken ct)
    {
        await api.UpdateCartItemAsync(ebookId, quantity, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid ebookId, CancellationToken ct)
    {
        await api.RemoveCartItemAsync(ebookId, ct);
        return RedirectToAction(nameof(Index));
    }
}
