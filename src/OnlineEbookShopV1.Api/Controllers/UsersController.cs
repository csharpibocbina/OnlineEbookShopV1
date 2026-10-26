using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/users")]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List() => Ok(await users.ListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Get(Guid id) => Ok(await users.GetAsync(id));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(UserCreateRequest request)
    {
        var created = await users.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, UserUpdateRequest request) =>
        Ok(await users.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await users.DeleteAsync(id);
        return NoContent();
    }
}
