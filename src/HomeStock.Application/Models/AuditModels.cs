using HomeStock.Domain.Enums;

namespace HomeStock.Application.Models;

public record AuditListDto(
    int Id,
    string LocationName,
    AuditStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int ItemCount,
    int DiscrepancyCount);

public record AuditItemDto(
    int Id,
    int AuditId,
    int ItemId,
    string ItemName,
    string? Barcode,
    string? ExpectedLocationName,
    AuditItemResult Result,
    int? MovedToLocationId,
    string? MovedToLocationName,
    string? Notes);

public record AuditDto(
    int Id,
    int LocationId,
    string LocationName,
    AuditStatus Status,
    bool IncludeSublocations,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? Notes,
    IReadOnlyList<AuditItemDto> Items)
{
    public int Total => Items.Count;
    public int Confirmed => Items.Count(i => i.Result == AuditItemResult.Confirmed);
    public int Pending => Items.Count(i => i.Result == AuditItemResult.Pending);
    public int Missing => Items.Count(i => i.Result == AuditItemResult.Missing);
    public int Moved => Items.Count(i => i.Result == AuditItemResult.Moved);
    public int Damaged => Items.Count(i => i.Result == AuditItemResult.Damaged);

    /// <summary>Rows that represent a discrepancy (anything not confirmed) once reviewed.</summary>
    public IEnumerable<AuditItemDto> Discrepancies =>
        Items.Where(i => i.Result is AuditItemResult.Missing or AuditItemResult.Moved or AuditItemResult.Damaged);
}
