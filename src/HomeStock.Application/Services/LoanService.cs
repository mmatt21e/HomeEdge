using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class LoanService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    ILogger<LoanService> logger) : ILoanService
{
    public async Task<IReadOnlyList<LoanDto>> GetForItemAsync(int itemId, CancellationToken ct = default) =>
        await db.Loans.AsNoTracking()
            .Where(l => l.ItemId == itemId)
            .OrderByDescending(l => l.LoanDate).ThenByDescending(l => l.Id)
            .Select(Project)
            .ToListAsync(ct);

    public async Task<LoanDto?> GetOpenLoanAsync(int itemId, CancellationToken ct = default) =>
        await db.Loans.AsNoTracking()
            .Where(l => l.ItemId == itemId && l.ActualReturnDate == null)
            .OrderByDescending(l => l.Id)
            .Select(Project)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<LoanDto>> GetOpenLoansAsync(CancellationToken ct = default)
    {
        var loans = await db.Loans.AsNoTracking()
            .Where(l => l.ActualReturnDate == null)
            .Select(Project)
            .ToListAsync(ct);
        // Overdue first, then by expected return date.
        return loans
            .OrderByDescending(l => l.IsOverdue)
            .ThenBy(l => l.ExpectedReturnDate ?? DateOnly.MaxValue)
            .ToList();
    }

    public async Task<Result<int>> LoanOutAsync(int itemId, LoanEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result<int>.Failure(errors);

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return Result<int>.Failure("Item not found.");

        if (await db.Loans.AnyAsync(l => l.ItemId == itemId && l.ActualReturnDate == null, ct))
            return Result<int>.Failure("This item is already loaned out. Record its return first.");

        if (model.ExpectedReturnDate is { } exp && exp < model.LoanDate)
            return Result<int>.Failure("Expected return date cannot be before the loan date.");

        var loan = new ItemLoan
        {
            ItemId = itemId,
            BorrowerName = model.BorrowerName.Trim(),
            BorrowerContact = model.BorrowerContact?.Trim(),
            LoanDate = model.LoanDate,
            ExpectedReturnDate = model.ExpectedReturnDate,
            Notes = model.Notes?.Trim()
        };
        db.Loans.Add(loan);

        item.Status = ItemStatus.Loaned;
        db.ItemHistory.Add(History(itemId, HistoryAction.Loaned, $"Loaned to {loan.BorrowerName}"));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Item {ItemId} loaned to {Borrower}", itemId, loan.BorrowerName);
        return Result<int>.Success(loan.Id);
    }

    public async Task<Result> ReturnAsync(int loanId, DateOnly? returnDate, string? notes, CancellationToken ct = default)
    {
        var loan = await db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, ct);
        if (loan is null) return Result.Failure("Loan not found.");
        if (loan.ActualReturnDate is not null) return Result.Failure("This loan is already marked returned.");

        var actual = returnDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (actual < loan.LoanDate) return Result.Failure("Return date cannot be before the loan date.");

        loan.ActualReturnDate = actual;
        if (!string.IsNullOrWhiteSpace(notes))
            loan.Notes = string.IsNullOrWhiteSpace(loan.Notes) ? notes.Trim() : $"{loan.Notes}\nReturned: {notes.Trim()}";

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == loan.ItemId, ct);
        if (item is not null && item.Status == ItemStatus.Loaned)
        {
            item.Status = ItemStatus.Available;
            db.ItemHistory.Add(History(loan.ItemId, HistoryAction.Returned, $"Returned by {loan.BorrowerName}"));
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private ItemHistory History(int itemId, HistoryAction action, string summary) => new()
    {
        ItemId = itemId,
        Action = action,
        UserId = currentUser.UserId,
        UserName = currentUser.UserName,
        Summary = summary,
        Timestamp = DateTime.UtcNow
    };

    private static readonly System.Linq.Expressions.Expression<Func<ItemLoan, LoanDto>> Project =
        l => new LoanDto(l.Id, l.ItemId, l.Item != null ? l.Item.Name : null, l.BorrowerName, l.BorrowerContact,
            l.LoanDate, l.ExpectedReturnDate, l.ActualReturnDate, l.Notes);
}
