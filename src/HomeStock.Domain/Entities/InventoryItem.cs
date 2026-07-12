using HomeStock.Domain.Common;
using HomeStock.Domain.Enums;

namespace HomeStock.Domain.Entities;

/// <summary>
/// The central inventory record. Deletion is soft (archival) by default so history and
/// audit references remain intact; hard delete is reserved for administrators.
/// </summary>
public class InventoryItem : BaseEntity, ISoftDeletable
{
    // --- Identity / description ---
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Notes { get; set; }

    // --- Classification ---
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Free-text sub-classification within a category (kept simple for Phase 1).</summary>
    public string? Subcategory { get; set; }

    public int Quantity { get; set; } = 1;

    // --- Product identifiers ---
    public string? Manufacturer { get; set; }
    public string? Brand { get; set; }
    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }

    /// <summary>Barcode / UPC / EAN value. Indexed for fast scan lookups.</summary>
    public string? Barcode { get; set; }

    // --- Purchase / valuation ---
    public DateOnly? PurchaseDate { get; set; }
    public string? PurchaseLocation { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? EstimatedValue { get; set; }
    public ItemCondition Condition { get; set; } = ItemCondition.Unknown;
    public DateOnly? WarrantyExpiration { get; set; }

    // --- Physical location ---
    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    /// <summary>Container / storage bin within the location (e.g. "Bin B12").</summary>
    public string? Container { get; set; }

    // --- Status ---
    public ItemStatus Status { get; set; } = ItemStatus.Available;

    // --- Soft delete / archival ---
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    // --- Relationships ---
    public ICollection<ItemTag> ItemTags { get; set; } = new List<ItemTag>();
    public ICollection<ItemAttachment> Attachments { get; set; } = new List<ItemAttachment>();
    public ICollection<ItemLoan> Loans { get; set; } = new List<ItemLoan>();
    public ICollection<ItemHistory> History { get; set; } = new List<ItemHistory>();
}
