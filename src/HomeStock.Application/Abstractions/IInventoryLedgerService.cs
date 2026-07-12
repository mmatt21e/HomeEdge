using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

/// <summary>
/// Records stock movements for an item and reports its position. Check-out/return move the
/// "currently out" tally; consume/restock/adjust change the on-hand quantity. Every movement is
/// appended to the ledger and to the item's change history.
/// </summary>
public interface IInventoryLedgerService
{
    Task<StockStatus?> GetStatusAsync(int itemId, CancellationToken ct = default);

    Task<IReadOnlyList<TransactionDto>> GetForItemAsync(int itemId, CancellationToken ct = default);

    /// <summary>Take an amount out to use (temporarily); reduces available, not on-hand.</summary>
    Task<Result> CheckOutAsync(int itemId, decimal quantity, string? note, int? projectId = null, CancellationToken ct = default);

    /// <summary>Put a checked-out amount back; restores available.</summary>
    Task<Result> ReturnAsync(int itemId, decimal quantity, string? note, int? projectId = null, CancellationToken ct = default);

    /// <summary>Use an amount up permanently; reduces on-hand.</summary>
    Task<Result> ConsumeAsync(int itemId, decimal quantity, string? note, int? projectId = null, CancellationToken ct = default);

    /// <summary>Add stock; increases on-hand.</summary>
    Task<Result> RestockAsync(int itemId, decimal quantity, string? note, CancellationToken ct = default);

    /// <summary>Correct the on-hand amount to an exact value.</summary>
    Task<Result> AdjustAsync(int itemId, decimal newOnHand, string? note, CancellationToken ct = default);
}
