using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OnlineEbookShopV1.Mvc.Api;

public class ShopApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class ShopApiClient(HttpClient http, IHttpContextAccessor accessor)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Task<AuthResponse> RegisterAsync(string email, string password, CancellationToken ct = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", new { email, password }, auth: false, ct);

    public Task<AuthResponse> LoginAsync(string email, string password, CancellationToken ct = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", new { email, password }, auth: false, ct);

    public Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/refresh", new { refreshToken }, auth: false, ct);

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        await SendAsync<object>(HttpMethod.Post, "api/auth/logout", new { refreshToken }, auth: true, ct);
    }

    public Task<List<EbookResponse>> ListEbooksAsync(CancellationToken ct = default) =>
        SendAsync<List<EbookResponse>>(HttpMethod.Get, "api/ebooks", null, auth: false, ct);

    public Task<EbookResponse> GetEbookAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<EbookResponse>(HttpMethod.Get, $"api/ebooks/{id}", null, auth: false, ct);

    public Task<EbookResponse> CreateEbookAsync(object body, CancellationToken ct = default) =>
        SendAsync<EbookResponse>(HttpMethod.Post, "api/ebooks", body, auth: true, ct);

    public Task<EbookResponse> UpdateEbookAsync(Guid id, object body, CancellationToken ct = default) =>
        SendAsync<EbookResponse>(HttpMethod.Put, $"api/ebooks/{id}", body, auth: true, ct);

    public Task DeleteEbookAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/ebooks/{id}", null, auth: true, ct);

    public Task<CartResponse> GetCartAsync(CancellationToken ct = default) =>
        SendAsync<CartResponse>(HttpMethod.Get, "api/cart", null, auth: true, ct);

    public Task<CartResponse> AddToCartAsync(Guid ebookId, int quantity = 1, CancellationToken ct = default) =>
        SendAsync<CartResponse>(HttpMethod.Post, "api/cart/items", new { ebookId, quantity }, auth: true, ct);

    public Task<CartResponse> UpdateCartItemAsync(Guid ebookId, int quantity, CancellationToken ct = default) =>
        SendAsync<CartResponse>(HttpMethod.Put, $"api/cart/items/{ebookId}", new { ebookId, quantity }, auth: true, ct);

    public Task<CartResponse> RemoveCartItemAsync(Guid ebookId, CancellationToken ct = default) =>
        SendAsync<CartResponse>(HttpMethod.Delete, $"api/cart/items/{ebookId}", null, auth: true, ct);

    public Task<CheckoutResponse> CheckoutAsync(string provider, string successUrl, string cancelUrl, CancellationToken ct = default) =>
        SendAsync<CheckoutResponse>(HttpMethod.Post, "api/checkout", new { provider, successUrl, cancelUrl }, auth: true, ct);

    public Task<OrderResponse> CapturePayPalAsync(string payPalOrderId, CancellationToken ct = default) =>
        SendAsync<OrderResponse>(HttpMethod.Post, "api/checkout/paypal/capture", new { payPalOrderId }, auth: true, ct);

    public Task<List<OrderResponse>> ListOrdersAsync(CancellationToken ct = default) =>
        SendAsync<List<OrderResponse>>(HttpMethod.Get, "api/orders", null, auth: true, ct);

    public Task<List<UserResponse>> ListUsersAsync(CancellationToken ct = default) =>
        SendAsync<List<UserResponse>>(HttpMethod.Get, "api/users", null, auth: true, ct);

    public Task<UserResponse> CreateUserAsync(object body, CancellationToken ct = default) =>
        SendAsync<UserResponse>(HttpMethod.Post, "api/users", body, auth: true, ct);

    public Task<UserResponse> UpdateUserAsync(Guid id, object body, CancellationToken ct = default) =>
        SendAsync<UserResponse>(HttpMethod.Put, $"api/users/{id}", body, auth: true, ct);

    public Task DeleteUserAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/users/{id}", null, auth: true, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool auth, CancellationToken ct)
    {
        var response = await SendOnceAsync(method, path, body, auth, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && auth)
        {
            if (await TryRefreshAsync(ct))
                response = await SendOnceAsync(method, path, body, auth, ct);
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = SafeDeserialize<ApiError>(json);
            throw new ShopApiException((int)response.StatusCode, err?.error ?? response.ReasonPhrase ?? "API request failed.");
        }

        if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(json))
            return default!;
        return JsonSerializer.Deserialize<T>(json, Json) ?? default!;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, object? body, bool auth, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path);
        if (auth)
        {
            var token = accessor.HttpContext is null ? null : AuthCookieStore.AccessToken(accessor.HttpContext.Request);
            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
            req.Content = JsonContent.Create(body);
        return await http.SendAsync(req, ct);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        var ctx = accessor.HttpContext;
        if (ctx is null) return false;
        var refresh = AuthCookieStore.RefreshToken(ctx.Request);
        if (string.IsNullOrEmpty(refresh)) return false;
        try
        {
            var auth = await RefreshAsync(refresh, ct);
            AuthCookieStore.Set(ctx.Response, auth);
            return true;
        }
        catch
        {
            AuthCookieStore.Clear(ctx.Response);
            return false;
        }
    }

    private static T? SafeDeserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch { return default; }
    }
}
