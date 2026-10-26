namespace OnlineEbookShopV1.Api.Services;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "OnlineEbookShopV1";
    public string Audience { get; set; } = "OnlineEbookShopV1Clients";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
