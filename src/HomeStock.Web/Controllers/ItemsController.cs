using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeStock.Web.Controllers;

/// <summary>
/// REST surface for inventory items, sharing the same application services as the Blazor UI.
/// Enables integrations (mobile scanners, scripts) without touching UI code.
/// </summary>
[ApiController]
[Route("api/items")]
[Authorize]
public class ItemsController(IItemService items) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ItemListDto>>> Search([FromQuery] ItemQuery query, CancellationToken ct)
        => Ok(await items.SearchAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ItemDto>> Get(int id, CancellationToken ct)
        => await items.GetAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpGet("by-barcode/{barcode}")]
    public async Task<ActionResult<ItemDto>> GetByBarcode(string barcode, CancellationToken ct)
        => await items.GetByBarcodeAsync(barcode, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost]
    [Authorize(Policy = Roles.CanEditPolicy)]
    public async Task<ActionResult> Create([FromBody] ItemEditModel model, CancellationToken ct)
    {
        var result = await items.CreateAsync(model, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = result.Value }, new { id = result.Value, warnings = result.Warnings })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Roles.CanEditPolicy)]
    public async Task<ActionResult> Update(int id, [FromBody] ItemEditModel model, CancellationToken ct)
    {
        var result = await items.UpdateAsync(id, model, ct);
        return result.Succeeded ? Ok(new { warnings = result.Warnings }) : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("{id:int}/archive")]
    [Authorize(Policy = Roles.CanEditPolicy)]
    public async Task<ActionResult> Archive(int id, CancellationToken ct)
        => (await items.ArchiveAsync(id, ct)).Succeeded ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Roles.AdminPolicy)]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
        => (await items.DeleteAsync(id, ct)).Succeeded ? NoContent() : NotFound();
}
