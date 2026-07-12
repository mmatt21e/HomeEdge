using HomeStock.Domain.Enums;

namespace HomeStock.Application.Models;

public class ReportFilter
{
    public int? CategoryId { get; set; }
    public int? LocationId { get; set; }
    public bool IncludeSublocations { get; set; } = true;
    public bool IncludeArchived { get; set; }
}

public record ReportRow(
    string Name,
    string? CategoryName,
    string? LocationName,
    string? Container,
    string? Manufacturer,
    string? ModelNumber,
    string? SerialNumber,
    string? Barcode,
    decimal Quantity,
    string? Unit,
    ItemCondition Condition,
    ItemStatus Status,
    DateOnly? PurchaseDate,
    decimal? PurchasePrice,
    decimal? EstimatedValue,
    DateOnly? WarrantyExpiration);

public record ReportDto(
    string FilterDescription,
    IReadOnlyList<ReportRow> Rows,
    int TotalRecords,
    decimal TotalQuantity,
    decimal TotalPurchasePrice,
    decimal TotalEstimatedValue);
