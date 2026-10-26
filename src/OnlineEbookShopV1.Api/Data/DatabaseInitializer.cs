using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Models;

namespace OnlineEbookShopV1.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:AutoMigrate", true))
            return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();
        await SeedAsync(db, config);
    }

    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            email = email.Trim().ToLowerInvariant();
            if (!await db.Users.AnyAsync(u => u.Email == email))
            {
                db.Users.Add(new AppUser
                {
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Role = Roles.Admin
                });
            }
        }

        if (!await db.Ebooks.AnyAsync())
        {
            db.Ebooks.AddRange(
                new Ebook { Title = "The Pragmatic Programmer", Author = "Andrew Hunt", Description = "Classic software craft.", Price = 29.99m, Isbn = "9780201616224" },
                new Ebook { Title = "Clean Code", Author = "Robert C. Martin", Description = "A handbook of agile software craftsmanship.", Price = 24.50m, Isbn = "9780132350884" },
                new Ebook { Title = "Domain-Driven Design", Author = "Eric Evans", Description = "Tackling complexity in the heart of software.", Price = 39.00m, Isbn = "9780321125217" });
        }

        await db.SaveChangesAsync();
    }
}
