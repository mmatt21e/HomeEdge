using HomeStock.Domain.Common;
using HomeStock.Domain.Enums;

namespace HomeStock.Domain.Entities;

/// <summary>
/// An inventory audit run scoped to a location. Captures the expected items at start and
/// records the confirmed/missing/moved/damaged outcome for each, producing a discrepancy
/// report on completion. Full audit history is retained.
/// </summary>
public class InventoryAudit : BaseEntity
{
    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    /// <summary>Identity user id of the person who ran the audit.</summary>
    public string? PerformedByUserId { get; set; }

    public AuditStatus Status { get; set; } = AuditStatus.InProgress;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>When true, child locations were included in the audit scope.</summary>
    public bool IncludeSublocations { get; set; }

    public string? Notes { get; set; }

    public ICollection<InventoryAuditItem> Items { get; set; } = new List<InventoryAuditItem>();
}

/// <summary>One expected item within an <see cref="InventoryAudit"/> and its recorded result.</summary>
public class InventoryAuditItem : BaseEntity
{
    public int AuditId { get; set; }
    public InventoryAudit Audit { get; set; } = null!;

    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;

    public AuditItemResult Result { get; set; } = AuditItemResult.Pending;

    /// <summary>If the item was found in a different location, the location it was moved to.</summary>
    public int? MovedToLocationId { get; set; }
    public Location? MovedToLocation { get; set; }

    public string? Notes { get; set; }
}
