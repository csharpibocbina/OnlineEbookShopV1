using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Tests.Services;

public class EbookServiceTests
{
    [Fact]
    public async Task Crud_round_trip()
    {
        await using var db = TestDb.Create();
        var sut = new EbookService(db);

        var created = await sut.CreateAsync(new EbookRequest(" Refactoring ", "Martin Fowler", null, null, 30m, null));
        var updated = await sut.UpdateAsync(created.Id, new EbookRequest("Refactoring 2e", "Martin Fowler", "New edition", null, 35m, null));
        await sut.DeleteAsync(created.Id);

        Assert.Equal("Refactoring", created.Title);
        Assert.Equal(35m, updated.Price);
        Assert.Empty(db.Ebooks);
    }

    [Fact]
    public async Task Unpublished_ebooks_are_hidden_from_shoppers_but_visible_to_admins()
    {
        await using var db = TestDb.Create();
        await db.AddEbookAsync("Visible");
        var hidden = await db.AddEbookAsync("Hidden", published: false);
        var sut = new EbookService(db);

        Assert.Equal(["Visible"], (await sut.ListAsync(publishedOnly: true)).Select(e => e.Title));
        Assert.Equal(2, (await sut.ListAsync(publishedOnly: false)).Count);
        await Assert.ThrowsAsync<AppException>(() => sut.GetAsync(hidden.Id, publishedOnly: true));
        Assert.Equal("Hidden", (await sut.GetAsync(hidden.Id, publishedOnly: false)).Title);
    }
}
