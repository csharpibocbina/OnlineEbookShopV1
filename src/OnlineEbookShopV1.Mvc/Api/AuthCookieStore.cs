namespace OnlineEbookShopV1.Mvc.Api;

public static class AuthCookieStore
{
    public const string Access = "shop.access";
    public const string Refresh = "shop.refresh";
    public const string Email = "shop.email";
    public const string Role = "shop.role";

    public static void Set(HttpResponse response, AuthResponse auth)
    {
        var opts = new CookieOptions { HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax };
        response.Cookies.Append(Access, auth.AccessToken, opts);
        response.Cookies.Append(Refresh, auth.RefreshToken, opts);
        response.Cookies.Append(Email, auth.Email, opts);
        response.Cookies.Append(Role, auth.Role, opts);
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Access);
        response.Cookies.Delete(Refresh);
        response.Cookies.Delete(Email);
        response.Cookies.Delete(Role);
    }

    public static string? AccessToken(HttpRequest request) => request.Cookies[Access];
    public static string? RefreshToken(HttpRequest request) => request.Cookies[Refresh];
    public static string? UserEmail(HttpRequest request) => request.Cookies[Email];
    public static string? UserRole(HttpRequest request) => request.Cookies[Role];
    public static bool IsSignedIn(HttpRequest request) => !string.IsNullOrEmpty(AccessToken(request));
    public static bool IsAdmin(HttpRequest request) => string.Equals(UserRole(request), "Admin", StringComparison.OrdinalIgnoreCase);
}
