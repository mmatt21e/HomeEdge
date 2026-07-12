using HomeStock.Application.Abstractions;
using HomeStock.Application.Models;
using HomeStock.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeStock.Web.Controllers;

/// <summary>Streaming download endpoints for CSV export and JSON backup.</summary>
[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController(IImportExportService importExport) : ControllerBase
{
    [HttpGet("items.csv")]
    public async Task<IActionResult> ItemsCsv([FromQuery] bool includeArchived, CancellationToken ct)
    {
        var file = await importExport.ExportItemsCsvAsync(new ItemQuery { IncludeArchived = includeArchived, PageSize = 200 }, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("backup.json")]
    [Authorize(Policy = Roles.AdminPolicy)]
    public async Task<IActionResult> Backup(CancellationToken ct)
    {
        var file = await importExport.ExportJsonBackupAsync(ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
