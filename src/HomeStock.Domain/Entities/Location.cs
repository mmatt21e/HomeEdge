using HomeStock.Domain.Common;

namespace HomeStock.Domain.Entities;

/// <summary>
/// A node in the hierarchical location tree (Home &gt; Garage &gt; Cabinet &gt; Drawer ...).
/// Self-referencing via <see cref="ParentId"/>. Each location owns an immutable
/// <see cref="Code"/> that its QR label encodes, so scanning opens the location contents.
/// </summary>
public class Location : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? ParentId { get; set; }
    public Location? Parent { get; set; }

    public ICollection<Location> Children { get; set; } = new List<Location>();

    /// <summary>
    /// Stable, unique, URL-safe code encoded into the location's QR label
    /// (e.g. "LOC-3F9A2B"). Generated on creation and never reused.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Relative path (under the attachment root) to the location photo, if any.</summary>
    public string? PhotoPath { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}
