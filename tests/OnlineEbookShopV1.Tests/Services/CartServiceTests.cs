using FakeItEasy;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Messaging;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Tests.Services;

public class CartServiceTests
{
    private readonly IEventPublisher _events = A.Fake<IEventPublisher>();

    [Fact]
    public async Task Add_new_item_then_same_item_increments_quantity()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync();
        var ebook = await db.AddEbookAsync(price: 12.50m);
        var sut = new CartService(db, _events);

        await sut.AddAsync(user.Id, new CartItemRequest(ebook.Id, 2));
        var cart = await sut.AddAsync(user.Id, new CartItemRequest(ebook.Id, 1));

        var line = Assert.Single(cart.Items);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(37.50m, cart.Total);
        Assert.Single(db.CartItems);
    }

    [Fact]
    public async Task Add_unpublished_or_missing_ebook_throws_404()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync();
        var hidden = await db.AddEbookAsync(published: false);
        var sut = new CartService(db, _events);

        var ex1 = await Assert.ThrowsAsync<AppException>(() => sut.AddAsync(user.Id, new CartItemRequest(hidden.Id)));
        var ex2 = await Assert.ThrowsAsync<AppException>(() => sut.AddAsync(user.Id, new CartItemRequest(Guid.NewGuid())));

        Assert.Equal(404, ex1.StatusCode);
        Assert.Equal(404, ex2.StatusCode);
    }

    [Fact]
    public async Task Update_sets_quantity_and_rejects_zero()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync();
        var ebook = await db.AddEbookAsync(price: 5m);
        var sut = new CartService(db, _events);
        await sut.AddAsync(user.Id, new CartItemRequest(ebook.Id));

        var cart = await sut.UpdateAsync(user.Id, ebook.Id, 4);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.UpdateAsync(user.Id, ebook.Id, 0));

        Assert.Equal(20m, cart.Total);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Remove_and_clear_empty_the_cart()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync();
        var first = await db.AddEbookAsync("One");
        var second = await db.AddEbookAsync("Two");
        var sut = new CartService(db, _events);
        await sut.AddAsync(user.Id, new CartItemRequest(first.Id));
        await sut.AddAsync(user.Id, new CartItemRequest(second.Id));

        var afterRemove = await sut.RemoveAsync(user.Id, first.Id);
        var afterClear = await sut.ClearAsync(user.Id);

        Assert.Equal(second.Id, Assert.Single(afterRemove.Items).EbookId);
        Assert.Empty(afterClear.Items);
        Assert.Empty(db.CartItems);
    }

    [Fact]
    public async Task Inactive_user_cannot_use_cart_and_invalid_account_is_published()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync(isActive: false);

        var ex = await Assert.ThrowsAsync<AppException>(() => new CartService(db, _events).GetAsync(user.Id));

        Assert.Equal(401, ex.StatusCode);
        A.CallTo(() => _events.PublishAsync(A<DomainEvent>.That.Matches(e => e.EventType == EventTypes.InvalidAccount && e.EntityId == user.Id), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
