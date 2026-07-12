using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

public interface ILoanService
{
    /// <summary>Full lending history for an item, newest first.</summary>
    Task<IReadOnlyList<LoanDto>> GetForItemAsync(int itemId, CancellationToken ct = default);

    /// <summary>The current open loan for an item, if any.</summary>
    Task<LoanDto?> GetOpenLoanAsync(int itemId, CancellationToken ct = default);

    /// <summary>All currently open loans across the inventory (overdue first).</summary>
    Task<IReadOnlyList<LoanDto>> GetOpenLoansAsync(CancellationToken ct = default);

    /// <summary>Loans an item out: records the loan, sets status to Loaned, writes history.</summary>
    Task<Result<int>> LoanOutAsync(int itemId, LoanEditModel model, CancellationToken ct = default);

    /// <summary>Marks a loan returned: stamps the return date, restores status, writes history.</summary>
    Task<Result> ReturnAsync(int loanId, DateOnly? returnDate, string? notes, CancellationToken ct = default);
}
