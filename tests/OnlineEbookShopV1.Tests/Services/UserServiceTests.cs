using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task Create_normalizes_email_and_role()
    {
        await using var db = TestDb.Create();
        var sut = new UserService(db);

        var created = await sut.CreateAsync(new UserCreateRequest(" Admin2@Test.local ", "Secret123!", "admin"));

        Assert.Equal("admin2@test.local", created.Email);
        Assert.Equal(Roles.Admin, created.Role);
        Assert.Equal(created, await sut.GetAsync(created.Id));
    }

    [Fact]
    public async Task Create_rejects_duplicate_email_and_unknown_role()
    {
        await using var db = TestDb.Create();
        await db.AddUserAsync("taken@test.local");
        var sut = new UserService(db);

        var dup = await Assert.ThrowsAsync<AppException>(() => sut.CreateAsync(new UserCreateRequest("taken@test.local", "Secret123!", "User")));
        var role = await Assert.ThrowsAsync<AppException>(() => sut.CreateAsync(new UserCreateRequest("new@test.local", "Secret123!", "Owner")));

        Assert.Equal(409, dup.StatusCode);
        Assert.Equal(400, role.StatusCode);
    }

    [Fact]
    public async Task List_returns_users_ordered_by_email()
    {
        await using var db = TestDb.Create();
        await db.AddUserAsync("b@test.local");
        await db.AddUserAsync("a@test.local");

        var users = await new UserService(db).ListAsync();

        Assert.Equal(["a@test.local", "b@test.local"], users.Select(u => u.Email));
    }

    [Fact]
    public async Task Update_changes_only_supplied_fields()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync("jay@test.local", "Secret123!");
        var oldHash = user.PasswordHash;

        var updated = await new UserService(db).UpdateAsync(user.Id, new UserUpdateRequest(null, null, "Admin", false));

        Assert.Equal("jay@test.local", updated.Email);
        Assert.Equal(Roles.Admin, updated.Role);
        Assert.False(updated.IsActive);
        Assert.Equal(oldHash, user.PasswordHash);
    }

    [Fact]
    public async Task Update_rejects_email_used_by_another_user()
    {
        await using var db = TestDb.Create();
        await db.AddUserAsync("one@test.local");
        var two = await db.AddUserAsync("two@test.local");

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            new UserService(db).UpdateAsync(two.Id, new UserUpdateRequest("one@test.local", null, null, null)));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_user_but_not_the_last_admin()
    {
        await using var db = TestDb.Create();
        var admin = await db.AddUserAsync("admin@test.local", role: Roles.Admin);
        var user = await db.AddUserAsync("user@test.local");
        var sut = new UserService(db);

        await sut.DeleteAsync(user.Id);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.DeleteAsync(admin.Id));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal([admin.Id], db.Users.Select(u => u.Id));
    }

    [Fact]
    public async Task Get_unknown_user_throws_404()
    {
        await using var db = TestDb.Create();

        var ex = await Assert.ThrowsAsync<AppException>(() => new UserService(db).GetAsync(Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
    }
}
