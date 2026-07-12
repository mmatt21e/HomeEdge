using HomeStock.Domain.Common;
using HomeStock.Domain.Enums;

namespace HomeStock.Domain.Entities;

/// <summary>
/// An append-only ledger entry recording a stock movement for an item: checking an amount out,
/// returning it, consuming it, restocking, or an adjustment. The item's on-hand
/// <see cref="InventoryItem.Quantity"/> reflects consume/restock/adjust; check-out/return move
/// the "currently out" tally (available = on-hand − currently out).
/// </summary>
public class InventoryTransaction : BaseEntity
{
    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;

    public TransactionType Type { get; set; }

    /// <summary>The (positive) amount moved, in the item's unit of measure.</summary>
    public decimal Quantity { get; set; }

    /// <summary>On-hand amount immediately after this transaction (for history display).</summary>
    public decimal BalanceAfter { get; set; }

    public string? Note { get; set; }

    /// <summary>Optional link to the project this movement belongs to (used from Step 3).</summary>
    public int? ProjectId { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    public DateTime Timestamp { get; set; }
}
