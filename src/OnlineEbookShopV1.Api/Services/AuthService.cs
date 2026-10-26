using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Data;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Messaging;
using OnlineEbookShopV1.Api.Models;

namespace OnlineEbookShopV1.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(string refreshToken);
    Task RevokeAsync(string refreshToken, Guid? actorId = null);
}

public class AuthService(AppDbContext db, ITokenService tokens, IEventPublisher events) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            throw AppException.Conflict("Email is already registered.");

        var user = new AppUser { Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), Role = Roles.User };
        db.Users.Add(user);
        return await IssueAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await events.PublishAsync(DomainEvents.Create(EventTypes.LoginFailed, "User", Guid.Empty, null, new { email }));
            throw AppException.Unauthorized("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            await events.PublishAsync(DomainEvents.Create(EventTypes.InvalidAccount, "User", user.Id, user.Id, new { email }));
            throw AppException.Unauthorized("Invalid user account.");
        }

        var auth = await IssueAsync(user);
        await events.PublishAsync(DomainEvents.Create(EventTypes.LoginSucceeded, "User", user.Id, user.Id, new { email }));
        return auth;
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is null || !stored.IsActive)
            throw AppException.Unauthorized("Invalid refresh token.");

        stored.RevokedAt = DateTime.UtcNow;
        var user = await db.Users.FirstAsync(u => u.Id == stored.UserId);
        if (!user.IsActive)
        {
            await db.SaveChangesAsync();
            await events.PublishAsync(DomainEvents.Create(EventTypes.InvalidAccount, "User", user.Id, user.Id, new { user.Email }));
            throw AppException.Unauthorized("Invalid user account.");
        }

        return await IssueAsync(user);
    }

    public async Task RevokeAsync(string refreshToken, Guid? actorId = null)
    {
        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await events.PublishAsync(DomainEvents.Create(EventTypes.Logout, "User", stored.UserId, actorId ?? stored.UserId, null));
        }
    }

    private async Task<AuthResponse> IssueAsync(AppUser user)
    {
        var (access, expires) = tokens.CreateAccessToken(user);
        var (raw, entity) = tokens.CreateRefreshToken(user.Id);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync();
        return new AuthResponse(access, raw, expires, user.Email, user.Role);
    }
}
