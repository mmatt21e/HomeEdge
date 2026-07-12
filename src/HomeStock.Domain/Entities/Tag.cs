using HomeStock.Domain.Common;

namespace HomeStock.Domain.Entities;

/// <summary>
/// A reusable label that can be applied to many items. Modelled as a proper entity with a
/// join table (<see cref="ItemTag"/>) rather than a comma-separated column.
/// </summary>
public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Normalised (lower-case, trimmed) form used for uniqueness and matching.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    public ICollection<ItemTag> ItemTags { get; set; } = new List<ItemTag>();
}

/// <summary>Join entity linking <see cref="InventoryItem"/> and <see cref="Tag"/>.</summary>
public class ItemTag
{
    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
