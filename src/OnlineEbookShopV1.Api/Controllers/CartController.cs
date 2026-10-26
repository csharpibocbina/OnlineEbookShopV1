using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public class CartController(ICartService cart) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartResponse>> Get() => Ok(await cart.GetAsync(this.UserId()));

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> Add(CartItemRequest request) =>
        Ok(await cart.AddAsync(this.UserId(), request));

    [HttpPut("items/{ebookId:guid}")]
    public async Task<ActionResult<CartResponse>> Update(Guid ebookId, [FromBody] CartItemRequest request) =>
        Ok(await cart.UpdateAsync(this.UserId(), ebookId, request.Quantity));

    [HttpDelete("items/{ebookId:guid}")]
    public async Task<ActionResult<CartResponse>> Remove(Guid ebookId) =>
        Ok(await cart.RemoveAsync(this.UserId(), ebookId));

    [HttpDelete]
    public async Task<ActionResult<CartResponse>> Clear() => Ok(await cart.ClearAsync(this.UserId()));
}
