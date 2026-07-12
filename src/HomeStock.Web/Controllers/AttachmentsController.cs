using HomeStock.Application.Abstractions;
using HomeStock.Application.Models;
using HomeStock.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeStock.Web.Controllers;

/// <summary>
/// Serves and manages item attachments. Binaries live outside wwwroot, so downloads go through
/// this authorized endpoint rather than static file serving.
/// </summary>
[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentsController(IAttachmentService attachments) : ControllerBase
{
    [HttpGet("item/{itemId:int}")]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> ForItem(int itemId, CancellationToken ct)
        => Ok(await attachments.GetForItemAsync(itemId, ct));

    [HttpGet("{id:int}/content")]
    public async Task<IActionResult> Content(int id, CancellationToken ct)
    {
        var opened = await attachments.OpenAsync(id, ct);
        if (opened is null) return NotFound();
        // inline so images render in the browser; the filename is used on save.
        return File(opened.Content, opened.ContentType, opened.FileName, enableRangeProcessing: true);
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var opened = await attachments.OpenAsync(id, ct);
        if (opened is null) return NotFound();
        return File(opened.Content, "application/octet-stream", opened.FileName);
    }

    [HttpPost("item/{itemId:int}")]
    [Authorize(Policy = Roles.CanEditPolicy)]
    [RequestSizeLimit(52_428_800)] // 50 MB request cap; per-file cap enforced by storage options
    public async Task<IActionResult> Upload(int itemId, [FromForm] IFormFile file,
        [FromForm] AttachmentType type = AttachmentType.Photo, [FromForm] string? description = null,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { errors = new[] { "No file provided." } });
        await using var stream = file.OpenReadStream();
        var result = await attachments.AddAsync(itemId, stream, file.FileName, file.ContentType, type, description, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(Content), new { id = result.Value }, new { id = result.Value })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Roles.CanEditPolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => (await attachments.DeleteAsync(id, ct)).Succeeded ? NoContent() : NotFound();
}
