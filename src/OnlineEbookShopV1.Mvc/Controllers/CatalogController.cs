using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Mvc.Api;

namespace OnlineEbookShopV1.Mvc.Controllers;

public class CatalogController(ShopApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            var ebooks = await api.ListEbooksAsync(ct);
            return View(ebooks);
        }
        catch (ShopApiException ex)
        {
            ViewBag.Error = ex.Message;
            return View(new List<EbookResponse>());
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        try
        {
            return View(await api.GetEbookAsync(id, ct));
        }
        catch (ShopApiException)
        {
            return NotFound();
        }
    }
}
