using HomeStock.Domain.Common;

namespace HomeStock.Domain.Entities;

/// <summary>
/// A record of an item being loaned out. A full history is kept: an open loan has a null
/// <see cref="ActualReturnDate"/>; returning the item stamps that date rather than deleting.
/// </summary>
public class ItemLoan : BaseEntity
{
    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;

    public string BorrowerName { get; set; } = string.Empty;

    public string? BorrowerContact { get; set; }

    public DateOnly LoanDate { get; set; }

    public DateOnly? ExpectedReturnDate { get; set; }

    public DateOnly? ActualReturnDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>Convenience flag: the loan is still open (item not yet returned).</summary>
    public bool IsOpen => ActualReturnDate is null;
}
