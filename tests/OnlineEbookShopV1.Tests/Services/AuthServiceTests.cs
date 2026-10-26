using FakeItEasy;
using Microsoft.Extensions.Options;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Messaging;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Tests.Services;

public class AuthServiceTests
{
    private readonly IEventPublisher _events = A.Fake<IEventPublisher>();
    private readonly TokenService _tokens = new(Options.Create(new JwtOptions { Key = new string('k', 40) }));

    private AuthService CreateSut(Api.Data.AppDbContext db) => new(db, _tokens, _events);

    private void AssertPublished(string eventType, int times = 1) =>
        A.CallTo(() => _events.PublishAsync(A<DomainEvent>.That.Matches(e => e.EventType == eventType), A<CancellationToken>._))
            .MustHaveHappened(times, Times.Exactly);

    [Fact]
    public async Task Login_with_valid_credentials_returns_tokens_and_publishes_login_succeeded()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync("jay@test.local", "Secret123!");

        var result = await CreateSut(db).LoginAsync(new LoginRequest("  JAY@test.local ", "Secret123!"));

        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal(Roles.User, result.Role);
        Assert.Single(db.RefreshTokens.Where(t => t.UserId == user.Id));
        AssertPublished(EventTypes.LoginSucceeded);
    }

    [Fact]
    public async Task Login_with_wrong_password_throws_401_and_publishes_login_failed()
    {
        await using var db = TestDb.Create();
        await db.AddUserAsync("jay@test.local", "Secret123!");

        var ex = await Assert.ThrowsAsync<AppException>(() => CreateSut(db).LoginAsync(new LoginRequest("jay@test.local", "wrong")));

        Assert.Equal(401, ex.StatusCode);
        AssertPublished(EventTypes.LoginFailed);
        AssertPublished(EventTypes.LoginSucceeded, 0);
    }

    [Fact]
    public async Task Login_with_unknown_email_publishes_login_failed()
    {
        await using var db = TestDb.Create();

        await Assert.ThrowsAsync<AppException>(() => CreateSut(db).LoginAsync(new LoginRequest("nobody@test.local", "x")));

        AssertPublished(EventTypes.LoginFailed);
    }

    [Fact]
    public async Task Login_to_inactive_account_throws_and_publishes_invalid_account()
    {
        await using var db = TestDb.Create();
        await db.AddUserAsync("jay@test.local", "Secret123!", isActive: false);

        var ex = await Assert.ThrowsAsync<AppException>(() => CreateSut(db).LoginAsync(new LoginRequest("jay@test.local", "Secret123!")));

        Assert.Equal(401, ex.StatusCode);
        AssertPublished(EventTypes.InvalidAccount);
        Assert.Empty(db.RefreshTokens);
    }

    [Fact]
    public async Task Register_creates_user_role_and_rejects_duplicate_email()
    {
        await using var db = TestDb.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegisterRequest("New@Test.local", "Secret123!"));
        var duplicate = await Assert.ThrowsAsync<AppException>(() => sut.RegisterAsync(new RegisterRequest("new@test.local", "Other123!")));

        Assert.Equal(Roles.User, result.Role);
        Assert.Equal("new@test.local", result.Email);
        Assert.Equal(409, duplicate.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_and_publishes_logout_once()
    {
        await using var db = TestDb.Create();
        await db.AddUserAsync("jay@test.local", "Secret123!");
        var sut = CreateSut(db);
        var login = await sut.LoginAsync(new LoginRequest("jay@test.local", "Secret123!"));

        await sut.RevokeAsync(login.RefreshToken);
        await sut.RevokeAsync(login.RefreshToken);

        Assert.All(db.RefreshTokens, t => Assert.NotNull(t.RevokedAt));
        AssertPublished(EventTypes.Logout);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_rejects_reuse()
    {
        await using var db = TestDb.Create();
        await db.AddUserAsync("jay@test.local", "Secret123!");
        var sut = CreateSut(db);
        var login = await sut.LoginAsync(new LoginRequest("jay@test.local", "Secret123!"));

        var refreshed = await sut.RefreshAsync(login.RefreshToken);
        var reuse = await Assert.ThrowsAsync<AppException>(() => sut.RefreshAsync(login.RefreshToken));

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(401, reuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_for_deactivated_user_publishes_invalid_account()
    {
        await using var db = TestDb.Create();
        var user = await db.AddUserAsync("jay@test.local", "Secret123!");
        var sut = CreateSut(db);
        var login = await sut.LoginAsync(new LoginRequest("jay@test.local", "Secret123!"));
        user.IsActive = false;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() => sut.RefreshAsync(login.RefreshToken));

        AssertPublished(EventTypes.InvalidAccount);
    }
}
