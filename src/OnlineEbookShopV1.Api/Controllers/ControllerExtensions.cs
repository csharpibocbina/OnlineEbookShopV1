using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Api.Models;

namespace OnlineEbookShopV1.Api.Controllers;

public static class ControllerExtensions
{
    public static Guid UserId(this ControllerBase c) => Guid.Parse(c.User.FindFirstValue("sub")!);

    public static bool IsAdmin(this ControllerBase c) => c.User.IsInRole(Roles.Admin);
}
