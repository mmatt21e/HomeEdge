using HomeStock.Domain.Common;

namespace HomeStock.Domain.Entities;

/// <summary>
/// A configurable classification for items (e.g. Tools, Electronics). Categories may be
/// system-seeded or user-created. Deletion is soft (archival) to preserve item references.
/// </summary>
public class Category : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Optional hex colour used for badges in the UI (e.g. "#4f46e5").</summary>
    public string? Color { get; set; }

    /// <summary>Optional icon key (Bootstrap Icons name) for the UI.</summary>
    public string? Icon { get; set; }

    /// <summary>True for categories created by the seed process; used to protect defaults.</summary>
    public bool IsSystem { get; set; }

    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}
