using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Models;

namespace OnlineEbookShopV1.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Ebook> Ebooks => Set<Ebook>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    public const string SchemaName = "ebookshop";

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(SchemaName);

        b.Entity<AppUser>(e =>
        {
            e.ToTable("users");
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Role).HasMaxLength(20);
            e.HasOne(x => x.Cart).WithOne(c => c.User).HasForeignKey<Cart>(c => c.UserId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Ignore(x => x.IsActive);
            e.HasOne<AppUser>().WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId);
        });

        b.Entity<Ebook>(e =>
        {
            e.ToTable("ebooks");
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Author).HasMaxLength(200);
            e.Property(x => x.Price).HasPrecision(10, 2);
        });

        b.Entity<Cart>(e =>
        {
            e.ToTable("carts");
            e.HasIndex(x => x.UserId).IsUnique();
        });

        b.Entity<CartItem>(e =>
        {
            e.ToTable("cart_items");
            e.HasIndex(x => new { x.CartId, x.EbookId }).IsUnique();
            e.HasOne<Cart>().WithMany(c => c.Items).HasForeignKey(x => x.CartId);
            e.HasOne(x => x.Ebook).WithMany().HasForeignKey(x => x.EbookId);
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.Property(x => x.Total).HasPrecision(10, 2);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne(x => x.User).WithMany(u => u.Orders).HasForeignKey(x => x.UserId);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.UnitPrice).HasPrecision(10, 2);
            e.HasOne<Order>().WithMany(o => o.Items).HasForeignKey(x => x.OrderId);
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Provider).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.ExternalId).HasMaxLength(200);
            e.HasIndex(x => x.ExternalId);
            e.HasOne(x => x.Order).WithMany(o => o.Payments).HasForeignKey(x => x.OrderId);
        });

        // Ids are assigned in code (Guid.NewGuid()), so EF must treat entities found
        // through navigations as new rather than as existing rows to update.
        foreach (var entity in b.Model.GetEntityTypes())
            entity.FindPrimaryKey()?.Properties.Single().ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
    }
}
