using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Data;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Models;

namespace OnlineEbookShopV1.Api.Services;

public interface IEbookService
{
    Task<IReadOnlyList<EbookResponse>> ListAsync(bool publishedOnly);
    Task<EbookResponse> GetAsync(Guid id, bool publishedOnly);
    Task<EbookResponse> CreateAsync(EbookRequest request);
    Task<EbookResponse> UpdateAsync(Guid id, EbookRequest request);
    Task DeleteAsync(Guid id);
}

public class EbookService(AppDbContext db) : IEbookService
{
    public async Task<IReadOnlyList<EbookResponse>> ListAsync(bool publishedOnly)
    {
        var q = db.Ebooks.AsNoTracking().AsQueryable();
        if (publishedOnly) q = q.Where(e => e.IsPublished);
        return await q.OrderBy(e => e.Title).Select(e => ToResponse(e)).ToListAsync();
    }

    public async Task<EbookResponse> GetAsync(Guid id, bool publishedOnly)
    {
        var ebook = await db.Ebooks.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id)
            ?? throw AppException.NotFound("Ebook not found.");
        if (publishedOnly && !ebook.IsPublished)
            throw AppException.NotFound("Ebook not found.");
        return ToResponse(ebook);
    }

    public async Task<EbookResponse> CreateAsync(EbookRequest request)
    {
        var ebook = FromRequest(new Ebook(), request);
        db.Ebooks.Add(ebook);
        await db.SaveChangesAsync();
        return ToResponse(ebook);
    }

    public async Task<EbookResponse> UpdateAsync(Guid id, EbookRequest request)
    {
        var ebook = await db.Ebooks.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw AppException.NotFound("Ebook not found.");
        FromRequest(ebook, request);
        await db.SaveChangesAsync();
        return ToResponse(ebook);
    }

    public async Task DeleteAsync(Guid id)
    {
        var ebook = await db.Ebooks.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw AppException.NotFound("Ebook not found.");
        db.Ebooks.Remove(ebook);
        await db.SaveChangesAsync();
    }

    private static Ebook FromRequest(Ebook ebook, EbookRequest request)
    {
        ebook.Title = request.Title.Trim();
        ebook.Author = request.Author.Trim();
        ebook.Description = request.Description;
        ebook.Isbn = request.Isbn;
        ebook.Price = request.Price;
        ebook.CoverUrl = request.CoverUrl;
        ebook.IsPublished = request.IsPublished;
        return ebook;
    }

    private static EbookResponse ToResponse(Ebook e) =>
        new(e.Id, e.Title, e.Author, e.Description, e.Isbn, e.Price, e.CoverUrl, e.IsPublished);
}
