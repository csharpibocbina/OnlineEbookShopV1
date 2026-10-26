namespace OnlineEbookShopV1.Mvc.Api;

public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, string Email, string Role);
public record UserResponse(Guid Id, string Email, string Role, bool IsActive, DateTime CreatedAt);
public record EbookResponse(Guid Id, string Title, string Author, string? Description, string? Isbn, decimal Price, string? CoverUrl, bool IsPublished);
public record CartItemResponse(Guid Id, Guid EbookId, string Title, decimal UnitPrice, int Quantity, decimal LineTotal);
public record CartResponse(Guid Id, IReadOnlyList<CartItemResponse> Items, decimal Total);
public record CheckoutResponse(Guid OrderId, Guid PaymentId, string RedirectUrl, string Provider);
public record OrderItemResponse(Guid EbookId, string Title, decimal UnitPrice, int Quantity);
public record PaymentResponse(Guid Id, string Provider, string Status, string? ExternalId, decimal Amount);
public record OrderResponse(Guid Id, string Status, decimal Total, DateTime CreatedAt, IReadOnlyList<OrderItemResponse> Items, IReadOnlyList<PaymentResponse> Payments);
public record ApiError(string? error);
