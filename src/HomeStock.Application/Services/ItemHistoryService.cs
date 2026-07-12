using HomeStock.Application.Abstractions;
using HomeStock.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeStock.Application.Services;

public class ItemHistoryService(IApplicationDbContext db) : IItemHistoryService
{
    public async Task<IReadOnlyList<ItemHistoryDto>> GetForItemAsync(int itemId, CancellationToken ct = default) =>
        await db.ItemHistory.AsNoTracking()
            .Where(h => h.ItemId == itemId)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new ItemHistoryDto(h.Id, h.ItemId, h.Action, h.UserName, h.Summary, h.ChangesJson, h.Timestamp))
            .ToListAsync(ct);
}
