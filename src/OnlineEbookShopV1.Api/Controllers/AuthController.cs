using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest r) => Ok(await auth.RegisterAsync(r));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest r) => Ok(await auth.LoginAsync(r));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest r) => Ok(await auth.RefreshAsync(r.RefreshToken));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest r)
    {
        Guid? actor = null;
        if (User.Identity?.IsAuthenticated == true)
            actor = this.UserId();
        await auth.RevokeAsync(r.RefreshToken, actor);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { id = this.UserId(), email = User.FindFirst("email")?.Value, role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value });
}
