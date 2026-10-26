using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Data;
using OnlineEbookShopV1.Api.Models;

namespace OnlineEbookShopV1.Tests;

internal static class TestDb
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static async Task<AppUser> AddUserAsync(this AppDbContext db, string email = "user@test.local",
        string password = "Password1!", string role = Roles.User, bool isActive = true)
    {
        var user = new AppUser { Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 4), Role = role, IsActive = isActive };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<Ebook> AddEbookAsync(this AppDbContext db, string title = "Clean Code", decimal price = 10m, bool published = true)
    {
        var ebook = new Ebook { Title = title, Author = "Author", Price = price, IsPublished = published };
        db.Ebooks.Add(ebook);
        await db.SaveChangesAsync();
        return ebook;
    }
}
