using HomeStock.Application.Abstractions;
using HomeStock.Application.Models;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeStock.Application.Services;

public class DashboardService(IApplicationDbContext db) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(ItemService.WarrantyWarningDays);

        var active = db.Items.AsNoTracking().Where(i => !i.IsArchived);

        var totalRecords = await active.CountAsync(ct);
        var totalQuantity = await active.SumAsync(i => (int?)i.Quantity, ct) ?? 0;
        var totalValue = await active.SumAsync(i => (decimal?)i.EstimatedValue, ct) ?? 0m;
        var loanedCount = await active.CountAsync(i => i.Status == ItemStatus.Loaned, ct);
        var missingPhoto = await active.CountAsync(i => !i.Attachments.Any(a => a.Type == AttachmentType.Photo), ct);
        var withoutLocation = await active.CountAsync(i => i.LocationId == null, ct);
        var expiringSoonCount = await active.CountAsync(
            i => i.WarrantyExpiration != null && i.WarrantyExpiration >= today && i.WarrantyExpiration <= soon, ct);

        var recentlyAdded = await active.OrderByDescending(i => i.CreatedAt).Take(5).Select(Project).ToListAsync(ct);
        var recentlyUpdated = await active.OrderByDescending(i => i.UpdatedAt).Take(5).Select(Project).ToListAsync(ct);
        var expiring = await active
            .Where(i => i.WarrantyExpiration != null && i.WarrantyExpiration >= today && i.WarrantyExpiration <= soon)
            .OrderBy(i => i.WarrantyExpiration).Take(10).Select(Project).ToListAsync(ct);
        var loaned = await active.Where(i => i.Status == ItemStatus.Loaned)
            .OrderBy(i => i.Name).Take(10).Select(Project).ToListAsync(ct);

        // Group by the raw (nullable) navigation name — SQL-translatable — then apply the
        // human-readable fallback label in memory after materialising.
        var byCategoryRaw = await active
            .GroupBy(i => i.Category != null ? i.Category.Name : null)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync(ct);
        var byCategory = byCategoryRaw.Select(x => new CountByName(x.Key ?? "Uncategorized", x.Count)).ToList();

        var byLocationRaw = await active
            .GroupBy(i => i.Location != null ? i.Location.Name : null)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync(ct);
        var byLocation = byLocationRaw.Select(x => new CountByName(x.Key ?? "No location", x.Count)).ToList();

        return new DashboardDto(totalRecords, totalQuantity, totalValue, loanedCount, missingPhoto,
            withoutLocation, expiringSoonCount, recentlyAdded, recentlyUpdated, expiring, loaned,
            byCategory, byLocation);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Domain.Entities.InventoryItem, ItemListDto>> Project =
        i => new ItemListDto(
            i.Id, i.Name, i.Category != null ? i.Category.Name : null,
            i.Location != null ? i.Location.Name : null, i.Quantity, i.Status, i.Condition,
            i.EstimatedValue, i.Barcode, i.IsArchived,
            i.Attachments.Any(a => a.Type == AttachmentType.Photo), i.WarrantyExpiration);
}
