using HomeStock.Domain.Common;
using HomeStock.Domain.Enums;

namespace HomeStock.Domain.Entities;

/// <summary>
/// An immutable change-history / audit-log entry for an item. Records the action, the user,
/// a timestamp, and a JSON snapshot of changed fields (previous vs new values). Never edited.
/// </summary>
public class ItemHistory : BaseEntity
{
    public int ItemId { get; set; }
    public InventoryItem? Item { get; set; }

    public HistoryAction Action { get; set; }

    /// <summary>Identity user id of the actor, if known.</summary>
    public string? UserId { get; set; }

    /// <summary>Display name of the actor captured at the time of the change.</summary>
    public string? UserName { get; set; }

    /// <summary>Human-readable summary of the change (e.g. "Status: Available → Loaned").</summary>
    public string? Summary { get; set; }

    /// <summary>JSON object of changed fields: { field: { old, new } }. Optional.</summary>
    public string? ChangesJson { get; set; }

    public DateTime Timestamp { get; set; }
}
