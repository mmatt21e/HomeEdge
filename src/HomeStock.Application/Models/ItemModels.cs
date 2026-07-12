using System.ComponentModel.DataAnnotations;
using HomeStock.Domain.Enums;

namespace HomeStock.Application.Models;

/// <summary>Full item projection for detail views.</summary>
public record ItemDto(
    int Id,
    string Name,
    string? Description,
    string? Notes,
    int? CategoryId,
    string? CategoryName,
    string? Subcategory,
    decimal Quantity,
    string? Unit,
    string? Manufacturer,
    string? Brand,
    string? ModelNumber,
    string? SerialNumber,
    string? Barcode,
    DateOnly? PurchaseDate,
    string? PurchaseLocation,
    decimal? PurchasePrice,
    decimal? EstimatedValue,
    ItemCondition Condition,
    DateOnly? WarrantyExpiration,
    int? LocationId,
    string? LocationName,
    string? Container,
    ItemStatus Status,
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<string> Tags,
    int AttachmentCount);

/// <summary>Condensed projection for list/search rows.</summary>
public record ItemListDto(
    int Id,
    string Name,
    string? CategoryName,
    string? LocationName,
    decimal Quantity,
    string? Unit,
    ItemStatus Status,
    ItemCondition Condition,
    decimal? EstimatedValue,
    string? Barcode,
    bool IsArchived,
    bool HasPhoto,
    DateOnly? WarrantyExpiration);

/// <summary>Create/edit input. Only Name is required so quick entry stays fast.</summary>
public class ItemEditModel
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(4000)]
    public string? Notes { get; set; }

    public int? CategoryId { get; set; }

    [StringLength(80)]
    public string? Subcategory { get; set; }

    [Range(0, 100_000_000)]
    public decimal Quantity { get; set; } = 1;

    /// <summary>Unit of measure (e.g. "ft", "m", "box"); blank means a plain count.</summary>
    [StringLength(20)]
    public string? Unit { get; set; }

    [StringLength(120)] public string? Manufacturer { get; set; }
    [StringLength(120)] public string? Brand { get; set; }
    [StringLength(120)] public string? ModelNumber { get; set; }
    [StringLength(120)] public string? SerialNumber { get; set; }
    [StringLength(120)] public string? Barcode { get; set; }

    public DateOnly? PurchaseDate { get; set; }
    [StringLength(200)] public string? PurchaseLocation { get; set; }

    [Range(0, 100_000_000)] public decimal? PurchasePrice { get; set; }
    [Range(0, 100_000_000)] public decimal? EstimatedValue { get; set; }

    public ItemCondition Condition { get; set; } = ItemCondition.Unknown;
    public DateOnly? WarrantyExpiration { get; set; }

    public int? LocationId { get; set; }
    [StringLength(120)] public string? Container { get; set; }

    public ItemStatus Status { get; set; } = ItemStatus.Available;

    /// <summary>Free-form tag names; resolved to Tag entities by the service.</summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>Suggested units of measure offered in the UI (free text is still allowed).</summary>
public static class CommonUnits
{
    public static readonly string[] All =
    {
        "ft", "in", "yd", "m", "cm", "mm",
        "box", "pack", "roll", "set", "pair",
        "gal", "qt", "L", "ml",
        "lb", "oz", "kg", "g"
    };

    /// <summary>Formats a quantity with its unit for display, e.g. "25 ft" or "×3".</summary>
    public static string Format(decimal quantity, string? unit)
    {
        var q = quantity == Math.Floor(quantity) ? quantity.ToString("0") : quantity.ToString("0.###");
        return string.IsNullOrWhiteSpace(unit) ? $"×{q}" : $"{q} {unit}";
    }
}

/// <summary>Warranty state derived from the expiration date and a warning window.</summary>
public enum WarrantyState { None, Active, ExpiringSoon, Expired }

/// <summary>Search + filter criteria for the items list.</summary>
public class ItemQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? LocationId { get; set; }
    /// <summary>Include items in descendant locations of <see cref="LocationId"/>.</summary>
    public bool IncludeSublocations { get; set; }
    public ItemCondition? Condition { get; set; }
    public ItemStatus? Status { get; set; }
    public WarrantyState? Warranty { get; set; }
    public DateOnly? PurchasedAfter { get; set; }
    public DateOnly? PurchasedBefore { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IncludeArchived { get; set; }
    public bool OnlyArchived { get; set; }
    public bool MissingPhoto { get; set; }
    public bool MissingLocation { get; set; }
    public bool OnlyLoaned { get; set; }
    public string? Tag { get; set; }

    public string SortBy { get; set; } = "updated";
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
