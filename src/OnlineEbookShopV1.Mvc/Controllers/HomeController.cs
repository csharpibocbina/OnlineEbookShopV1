using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Mvc.Api;
using OnlineEbookShopV1.Mvc.Models;

namespace OnlineEbookShopV1.Mvc.Controllers;

public class HomeController(ShopApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            return View(await api.ListEbooksAsync(ct));
        }
        catch (ShopApiException ex)
        {
            ViewBag.Error = ex.Message;
            return View(new List<EbookResponse>());
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
