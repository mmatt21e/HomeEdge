using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class InventoryLedgerService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    ILogger<InventoryLedgerService> logger) : IInventoryLedgerService
{
    public async Task<StockStatus?> GetStatusAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.Items.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.Quantity, i.Unit })
            .FirstOrDefaultAsync(ct);
        if (item is null) return null;

        var checkedOut = await ComputeCheckedOutAsync(itemId, ct);
        return new StockStatus(item.Quantity, checkedOut, item.Unit);
    }

    public async Task<IReadOnlyList<TransactionDto>> GetForItemAsync(int itemId, CancellationToken ct = default)
    {
        var unit = await db.Items.AsNoTracking().Where(i => i.Id == itemId).Select(i => i.Unit).FirstOrDefaultAsync(ct);
        return await db.Transactions.AsNoTracking()
            .Where(t => t.ItemId == itemId)
            .OrderByDescending(t => t.Timestamp).ThenByDescending(t => t.Id)
            .Select(t => new TransactionDto(t.Id, t.ItemId, t.Type, t.Quantity, t.BalanceAfter, unit, t.Note, t.ProjectId, t.UserName, t.Timestamp))
            .ToListAsync(ct);
    }

    public async Task<Result> CheckOutAsync(int itemId, decimal quantity, string? note, int? projectId = null, CancellationToken ct = default)
    {
        if (quantity <= 0) return Result.Failure("Enter an amount greater than zero.");
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return Result.Failure("Item not found.");

        var available = item.Quantity - await ComputeCheckedOutAsync(itemId, ct);
        if (quantity > available)
            return Result.Failure($"Only {CommonUnits.Format(available, item.Unit)} available to take out.");

        Record(item, TransactionType.CheckOut, quantity, item.Quantity, note, projectId, $"Checked out {CommonUnits.Format(quantity, item.Unit)}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReturnAsync(int itemId, decimal quantity, string? note, int? projectId = null, CancellationToken ct = default)
    {
        if (quantity <= 0) return Result.Failure("Enter an amount greater than zero.");
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return Result.Failure("Item not found.");

        var checkedOut = await ComputeCheckedOutAsync(itemId, ct);
        if (quantity > checkedOut)
            return Result.Failure($"Only {CommonUnits.Format(checkedOut, item.Unit)} is currently checked out.");

        Record(item, TransactionType.Return, quantity, item.Quantity, note, projectId, $"Returned {CommonUnits.Format(quantity, item.Unit)}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ConsumeAsync(int itemId, decimal quantity, string? note, int? projectId = null, CancellationToken ct = default)
    {
        if (quantity <= 0) return Result.Failure("Enter an amount greater than zero.");
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return Result.Failure("Item not found.");
        if (quantity > item.Quantity)
            return Result.Failure($"Cannot use {CommonUnits.Format(quantity, item.Unit)} — only {CommonUnits.Format(item.Quantity, item.Unit)} on hand.");

        item.Quantity -= quantity;
        Record(item, TransactionType.Consume, quantity, item.Quantity, note, projectId, $"Used {CommonUnits.Format(quantity, item.Unit)}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Item {ItemId} consumed {Qty}; on-hand now {OnHand}", itemId, quantity, item.Quantity);
        return Result.Success();
    }

    public async Task<Result> RestockAsync(int itemId, decimal quantity, string? note, CancellationToken ct = default)
    {
        if (quantity <= 0) return Result.Failure("Enter an amount greater than zero.");
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return Result.Failure("Item not found.");

        item.Quantity += quantity;
        Record(item, TransactionType.Restock, quantity, item.Quantity, note, null, $"Restocked {CommonUnits.Format(quantity, item.Unit)}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> AdjustAsync(int itemId, decimal newOnHand, string? note, CancellationToken ct = default)
    {
        if (newOnHand < 0) return Result.Failure("On-hand amount cannot be negative.");
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return Result.Failure("Item not found.");

        var delta = Math.Abs(newOnHand - item.Quantity);
        item.Quantity = newOnHand;
        Record(item, TransactionType.Adjust, delta, item.Quantity, note, null, $"Adjusted to {CommonUnits.Format(newOnHand, item.Unit)}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // "Currently out" nets check-outs against returns and consumption (what you took and used
    // is gone, so it stops counting as out). Clamped at zero.
    private async Task<decimal> ComputeCheckedOutAsync(int itemId, CancellationToken ct)
    {
        var txns = await db.Transactions.AsNoTracking()
            .Where(t => t.ItemId == itemId)
            .Select(t => new { t.Type, t.Quantity })
            .ToListAsync(ct);

        decimal Sum(TransactionType type) => txns.Where(t => t.Type == type).Sum(t => t.Quantity);
        var outstanding = Sum(TransactionType.CheckOut) - Sum(TransactionType.Return) - Sum(TransactionType.Consume);
        return Math.Max(0, outstanding);
    }

    private void Record(InventoryItem item, TransactionType type, decimal quantity, decimal balanceAfter,
        string? note, int? projectId, string historySummary)
    {
        db.Transactions.Add(new InventoryTransaction
        {
            ItemId = item.Id,
            Type = type,
            Quantity = quantity,
            BalanceAfter = balanceAfter,
            Note = note?.Trim(),
            ProjectId = projectId,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Timestamp = DateTime.UtcNow
        });

        db.ItemHistory.Add(new ItemHistory
        {
            ItemId = item.Id,
            Action = HistoryAction.Updated,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Summary = historySummary,
            Timestamp = DateTime.UtcNow
        });
    }
}
