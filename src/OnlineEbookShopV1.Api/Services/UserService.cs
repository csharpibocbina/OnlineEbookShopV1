using Microsoft.EntityFrameworkCore;
using OnlineEbookShopV1.Api.Data;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Models;

namespace OnlineEbookShopV1.Api.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> ListAsync();
    Task<UserResponse> GetAsync(Guid id);
    Task<UserResponse> CreateAsync(UserCreateRequest request);
    Task<UserResponse> UpdateAsync(Guid id, UserUpdateRequest request);
    Task DeleteAsync(Guid id);
}

public class UserService(AppDbContext db) : IUserService
{
    public async Task<IReadOnlyList<UserResponse>> ListAsync() =>
        await db.Users.AsNoTracking().OrderBy(u => u.Email).Select(u => ToResponse(u)).ToListAsync();

    public async Task<UserResponse> GetAsync(Guid id)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
            ?? throw AppException.NotFound("User not found.");
        return ToResponse(user);
    }

    public async Task<UserResponse> CreateAsync(UserCreateRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            throw AppException.Conflict("Email is already registered.");
        var role = NormalizeRole(request.Role);
        var user = new AppUser
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            IsActive = request.IsActive
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return ToResponse(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UserUpdateRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw AppException.NotFound("User not found.");

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id))
                throw AppException.Conflict("Email is already registered.");
            user.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        if (!string.IsNullOrWhiteSpace(request.Role))
            user.Role = NormalizeRole(request.Role);

        if (request.IsActive is { } active)
            user.IsActive = active;

        await db.SaveChangesAsync();
        return ToResponse(user);
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw AppException.NotFound("User not found.");
        if (user.Role == Roles.Admin)
        {
            var adminCount = await db.Users.CountAsync(u => u.Role == Roles.Admin);
            if (adminCount <= 1)
                throw AppException.BadRequest("Cannot delete the last admin.");
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    private static string NormalizeRole(string role) =>
        role.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase) ? Roles.Admin
        : role.Equals(Roles.User, StringComparison.OrdinalIgnoreCase) ? Roles.User
        : throw AppException.BadRequest("Role must be Admin or User.");

    private static UserResponse ToResponse(AppUser u) => new(u.Id, u.Email, u.Role, u.IsActive, u.CreatedAt);
}
