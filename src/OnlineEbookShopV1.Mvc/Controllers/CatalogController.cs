using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Mvc.Api;

namespace OnlineEbookShopV1.Mvc.Controllers;

public class CatalogController(ShopApiClient api) : Controller
{
    public async Task<IActionResult> Index(string? q, decimal? minPrice, decimal? maxPrice, string? sort, CancellationToken ct)
    {
        ViewBag.Q = q;
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.Sort = sort;
        try
        {
            IEnumerable<EbookResponse> ebooks = await api.ListEbooksAsync(ct);
            if (!string.IsNullOrWhiteSpace(q))
                ebooks = ebooks.Where(e =>
                    e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    e.Author.Contains(q, StringComparison.OrdinalIgnoreCase));
            if (minPrice is not null) ebooks = ebooks.Where(e => e.Price >= minPrice);
            if (maxPrice is not null) ebooks = ebooks.Where(e => e.Price <= maxPrice);
            ebooks = sort switch
            {
                "price_asc" => ebooks.OrderBy(e => e.Price),
                "price_desc" => ebooks.OrderByDescending(e => e.Price),
                "title" => ebooks.OrderBy(e => e.Title),
                _ => ebooks
            };
            return View(ebooks.ToList());
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
            var ebook = await api.GetEbookAsync(id, ct);
            try
            {
                ViewBag.Related = (await api.ListEbooksAsync(ct)).Where(e => e.Id != id).Take(4).ToList();
            }
            catch (ShopApiException)
            {
                ViewBag.Related = new List<EbookResponse>();
            }
            return View(ebook);
        }
        catch (ShopApiException)
        {
            return NotFound();
        }
    }
}
