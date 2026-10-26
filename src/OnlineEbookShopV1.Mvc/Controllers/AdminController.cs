using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Mvc.Api;
using OnlineEbookShopV1.Mvc.Models;

namespace OnlineEbookShopV1.Mvc.Controllers;

public class AdminController(ShopApiClient api) : Controller
{
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        return View(await api.ListUsersAsync(ct));
    }

    [HttpGet]
    public IActionResult CreateUser()
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        return View(new UserEditViewModel { Role = "User", IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(UserEditViewModel model, CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        if (!ModelState.IsValid) return View(model);
        await api.CreateUserAsync(new { model.Email, model.Password, model.Role, model.IsActive }, ct);
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(Guid id, CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        var users = await api.ListUsersAsync(ct);
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return NotFound();
        return View(new UserEditViewModel { Id = user.Id, Email = user.Email, Role = user.Role, IsActive = user.IsActive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(UserEditViewModel model, CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        await api.UpdateUserAsync(model.Id, new { model.Email, password = string.IsNullOrWhiteSpace(model.Password) ? null : model.Password, model.Role, model.IsActive }, ct);
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        await api.DeleteUserAsync(id, ct);
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Ebooks(CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        return View(await api.ListEbooksAsync(ct));
    }

    [HttpGet]
    public IActionResult CreateEbook()
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        return View(new EbookEditViewModel { IsPublished = true, Price = 9.99m });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEbook(EbookEditViewModel model, CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        await api.CreateEbookAsync(model, ct);
        return RedirectToAction(nameof(Ebooks));
    }

    [HttpGet]
    public async Task<IActionResult> EditEbook(Guid id, CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        var ebook = await api.GetEbookAsync(id, ct);
        return View(new EbookEditViewModel
        {
            Id = ebook.Id,
            Title = ebook.Title,
            Author = ebook.Author,
            Description = ebook.Description,
            Isbn = ebook.Isbn,
            Price = ebook.Price,
            CoverUrl = ebook.CoverUrl,
            IsPublished = ebook.IsPublished
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEbook(EbookEditViewModel model, CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        await api.UpdateEbookAsync(model.Id, model, ct);
        return RedirectToAction(nameof(Ebooks));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEbook(Guid id, CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        await api.DeleteEbookAsync(id, ct);
        return RedirectToAction(nameof(Ebooks));
    }

    public async Task<IActionResult> Orders(CancellationToken ct)
    {
        if (!EnsureAdmin()) return RedirectToLogin();
        return View(await api.ListOrdersAsync(ct));
    }

    private bool EnsureAdmin() => AuthCookieStore.IsAdmin(Request);
    private IActionResult RedirectToLogin() => RedirectToAction("Login", "Account", new { returnUrl = Request.Path.Value });
}
