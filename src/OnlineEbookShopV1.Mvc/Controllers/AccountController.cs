using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Mvc.Api;
using OnlineEbookShopV1.Mvc.Models;

namespace OnlineEbookShopV1.Mvc.Controllers;

public class AccountController(ShopApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            var auth = await api.LoginAsync(model.Email, model.Password, ct);
            AuthCookieStore.Set(Response, auth);
            return Redirect(string.IsNullOrWhiteSpace(model.ReturnUrl) ? "/" : model.ReturnUrl);
        }
        catch (ShopApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            var auth = await api.RegisterAsync(model.Email, model.Password, ct);
            AuthCookieStore.Set(Response, auth);
            return RedirectToAction("Index", "Catalog");
        }
        catch (ShopApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refresh = AuthCookieStore.RefreshToken(Request);
        if (!string.IsNullOrEmpty(refresh))
        {
            try { await api.LogoutAsync(refresh, ct); } catch (ShopApiException) { /* still clear local cookies */ }
        }

        AuthCookieStore.Clear(Response);
        return RedirectToAction("Index", "Catalog");
    }
}
