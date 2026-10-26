using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineEbookShopV1.Api.Dtos;
using OnlineEbookShopV1.Api.Models;
using OnlineEbookShopV1.Api.Services;

namespace OnlineEbookShopV1.Api.Controllers;

[ApiController]
[Route("api/ebooks")]
public class EbooksController(IEbookService ebooks) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EbookResponse>>> List() =>
        Ok(await ebooks.ListAsync(publishedOnly: !this.IsAdmin()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EbookResponse>> Get(Guid id) =>
        Ok(await ebooks.GetAsync(id, publishedOnly: !this.IsAdmin()));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<EbookResponse>> Create(EbookRequest request)
    {
        var created = await ebooks.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EbookResponse>> Update(Guid id, EbookRequest request) =>
        Ok(await ebooks.UpdateAsync(id, request));

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await ebooks.DeleteAsync(id);
        return NoContent();
    }
}
