using System.ComponentModel.DataAnnotations;

namespace OnlineEbookShopV1.Mvc.Models;

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
}

public class UserEditViewModel
{
    public Guid Id { get; set; }
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [DataType(DataType.Password)] public string? Password { get; set; }
    [Required] public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
}

public class EbookEditViewModel
{
    public Guid Id { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    [Required] public string Author { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Isbn { get; set; }
    [Range(0.01, 100000)] public decimal Price { get; set; }
    public string? CoverUrl { get; set; }
    public bool IsPublished { get; set; } = true;
}
