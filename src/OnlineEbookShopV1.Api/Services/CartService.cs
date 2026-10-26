using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Data;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Messaging;
using OnlineEbookShopV1.Api.Models;

namespace OnlineEbookShopV1.Api.Services;

public interface ICartService
{
    Task<CartResponse> GetAsync(Guid userId);
    Task<CartResponse> AddAsync(Guid userId, CartItemRequest request);
    Task<CartResponse> UpdateAsync(Guid userId, Guid ebookId, int quantity);
    Task<CartResponse> RemoveAsync(Guid userId, Guid ebookId);
    Task<CartResponse> ClearAsync(Guid userId);
}

public class CartService(AppDbContext db, IEventPublisher events) : ICartService
{
    public async Task<CartResponse> GetAsync(Guid userId)
    {
        await EnsureActiveUserAsync(userId);
        var cart = await LoadCartAsync(userId, create: true);
        return ToResponse(cart);
    }

    public async Task<CartResponse> AddAsync(Guid userId, CartItemRequest request)
    {
        await EnsureActiveUserAsync(userId);
        var ebook = await db.Ebooks.FirstOrDefaultAsync(e => e.Id == request.EbookId && e.IsPublished)
            ?? throw AppException.NotFound("Ebook not found.");
        var cart = await LoadCartAsync(userId, create: true);
        var item = cart.Items.FirstOrDefault(i => i.EbookId == ebook.Id);
        if (item is null)
            cart.Items.Add(new CartItem { EbookId = ebook.Id, Ebook = ebook, Quantity = request.Quantity });
        else
            item.Quantity += request.Quantity;
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(cart);
    }

    public async Task<CartResponse> UpdateAsync(Guid userId, Guid ebookId, int quantity)
    {
        await EnsureActiveUserAsync(userId);
        if (quantity < 1) throw AppException.BadRequest("Quantity must be at least 1.");
        var cart = await LoadCartAsync(userId, create: true);
        var item = cart.Items.FirstOrDefault(i => i.EbookId == ebookId)
            ?? throw AppException.NotFound("Cart item not found.");
        item.Quantity = quantity;
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(cart);
    }

    public async Task<CartResponse> RemoveAsync(Guid userId, Guid ebookId)
    {
        await EnsureActiveUserAsync(userId);
        var cart = await LoadCartAsync(userId, create: true);
        var item = cart.Items.FirstOrDefault(i => i.EbookId == ebookId)
            ?? throw AppException.NotFound("Cart item not found.");
        cart.Items.Remove(item);
        db.CartItems.Remove(item);
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(cart);
    }

    public async Task<CartResponse> ClearAsync(Guid userId)
    {
        await EnsureActiveUserAsync(userId);
        var cart = await LoadCartAsync(userId, create: true);
        db.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(cart);
    }

    private async Task EnsureActiveUserAsync(Guid userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !user.IsActive)
        {
            await events.PublishAsync(DomainEvents.Create(EventTypes.InvalidAccount, "User", userId, userId, null));
            throw AppException.Unauthorized("Invalid user account.");
        }
    }

    private async Task<Cart> LoadCartAsync(Guid userId, bool create)
    {
        var cart = await db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Ebook)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart is null)
        {
            if (!create) throw AppException.NotFound("Cart not found.");
            cart = new Cart { UserId = userId };
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
        }

        return cart;
    }

    private static CartResponse ToResponse(Cart cart)
    {
        var items = cart.Items
            .Where(i => i.Ebook is not null)
            .Select(i => new CartItemResponse(i.Id, i.EbookId, i.Ebook!.Title, i.Ebook.Price, i.Quantity, i.Ebook.Price * i.Quantity))
            .ToList();
        return new CartResponse(cart.Id, items, items.Sum(i => i.LineTotal));
    }
}
