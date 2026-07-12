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

    public async Task<IReadOnlyList<RecentActivityDto>> GetRecentAsync(int limit = 25, CancellationToken ct = default) =>
        await db.ItemHistory.AsNoTracking()
            .OrderByDescending(h => h.Timestamp)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(h => new RecentActivityDto(h.Id, h.ItemId,
                h.Item != null ? h.Item.Name : null, h.Action, h.UserName, h.Summary, h.Timestamp))
            .ToListAsync(ct);
}
