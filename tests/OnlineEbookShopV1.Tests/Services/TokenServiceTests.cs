using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Tests.Services;

public class TokenServiceTests
{
    private readonly TokenService _sut = new(Options.Create(new JwtOptions
    {
        Key = new string('k', 40),
        Issuer = "issuer",
        Audience = "audience",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    }));

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.User)]
    public void Access_token_carries_user_id_email_and_role(string role)
    {
        var user = new AppUser { Email = "jay@test.local", Role = role };

        var (token, expires) = _sut.CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("issuer", jwt.Issuer);
        Assert.Contains("audience", jwt.Audiences);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == role);
        Assert.InRange(expires, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void Refresh_token_is_stored_only_as_hash()
    {
        var userId = Guid.NewGuid();

        var (raw, entity) = _sut.CreateRefreshToken(userId);

        Assert.Equal(userId, entity.UserId);
        Assert.NotEqual(raw, entity.TokenHash);
        Assert.Equal(_sut.Hash(raw), entity.TokenHash);
        Assert.True(entity.IsActive);
    }
}
