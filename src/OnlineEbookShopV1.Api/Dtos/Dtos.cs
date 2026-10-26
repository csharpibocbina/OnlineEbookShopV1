using System.ComponentModel.DataAnnotations;

namespace OnlineEbookShopV1.Api.Dtos;

public record RegisterRequest([Required, EmailAddress] string Email, [Required, MinLength(8)] string Password);
public record LoginRequest([Required] string Email, [Required] string Password);
public record RefreshRequest([Required] string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, string Email, string Role);

public record UserCreateRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string Role,
    bool IsActive = true);

public record UserUpdateRequest(
    [EmailAddress] string? Email,
    string? Password,
    string? Role,
    bool? IsActive);

public record UserResponse(Guid Id, string Email, string Role, bool IsActive, DateTime CreatedAt);

public record EbookRequest(
    [Required, StringLength(300)] string Title,
    [Required, StringLength(200)] string Author,
    string? Description,
    string? Isbn,
    [Range(0.01, 100000)] decimal Price,
    string? CoverUrl,
    bool IsPublished = true);

public record EbookResponse(Guid Id, string Title, string Author, string? Description, string? Isbn, decimal Price, string? CoverUrl, bool IsPublished);

public record CartItemRequest([Required] Guid EbookId, [Range(1, 99)] int Quantity = 1);
public record CartItemResponse(Guid Id, Guid EbookId, string Title, decimal UnitPrice, int Quantity, decimal LineTotal);
public record CartResponse(Guid Id, IReadOnlyList<CartItemResponse> Items, decimal Total);

public record CheckoutRequest(
    [Required] string Provider,
    [Required] string SuccessUrl,
    [Required] string CancelUrl);

public record PayPalCaptureRequest([Required] string PayPalOrderId);

public record CheckoutResponse(Guid OrderId, Guid PaymentId, string RedirectUrl, string Provider);

public record OrderItemResponse(Guid EbookId, string Title, decimal UnitPrice, int Quantity);
public record PaymentResponse(Guid Id, string Provider, string Status, string? ExternalId, decimal Amount);
public record OrderResponse(Guid Id, string Status, decimal Total, DateTime CreatedAt, IReadOnlyList<OrderItemResponse> Items, IReadOnlyList<PaymentResponse> Payments);
