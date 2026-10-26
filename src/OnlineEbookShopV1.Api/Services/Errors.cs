namespace OnlineEbookShopV1.Api.Services;

public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public static AppException NotFound(string m) => new(404, m);
    public static AppException Conflict(string m) => new(409, m);
    public static AppException Unauthorized(string m) => new(401, m);
    public static AppException Forbidden(string m) => new(403, m);
    public static AppException BadRequest(string m) => new(400, m);
    public static AppException ServiceUnavailable(string m) => new(503, m);
}
