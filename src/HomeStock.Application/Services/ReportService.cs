using HomeStock.Application.Abstractions;
using HomeStock.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeStock.Application.Services;

public class ReportService(IApplicationDbContext db, ILocationService locations) : IReportService
{
    public async Task<ReportDto> GenerateAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var query = db.Items.AsNoTracking().Include(i => i.Category).Include(i => i.Location).AsQueryable();

        if (!filter.IncludeArchived) query = query.Where(i => !i.IsArchived);
        if (filter.CategoryId is int cat) query = query.Where(i => i.CategoryId == cat);

        var filterParts = new List<string>();
        if (filter.LocationId is int loc)
        {
            if (filter.IncludeSublocations)
            {
                var ids = await locations.GetSelfAndDescendantIdsAsync(loc, ct);
                query = query.Where(i => i.LocationId != null && ids.Contains(i.LocationId.Value));
            }
            else query = query.Where(i => i.LocationId == loc);
            var locName = (await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == loc, ct))?.Name;
            filterParts.Add($"Location: {locName}{(filter.IncludeSublocations ? " (incl. sub-locations)" : "")}");
        }
        if (filter.CategoryId is int c)
        {
            var catName = (await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c, ct))?.Name;
            filterParts.Add($"Category: {catName}");
        }
        if (filter.IncludeArchived) filterParts.Add("Including archived");

        var rows = await query
            .OrderBy(i => i.Location != null ? i.Location.Name : "")
            .ThenBy(i => i.Name)
            .Select(i => new ReportRow(
                i.Name,
                i.Category != null ? i.Category.Name : null,
                i.Location != null ? i.Location.Name : null,
                i.Container, i.Manufacturer, i.ModelNumber, i.SerialNumber, i.Barcode,
                i.Quantity, i.Condition, i.Status,
                i.PurchaseDate, i.PurchasePrice, i.EstimatedValue, i.WarrantyExpiration))
            .ToListAsync(ct);

        return new ReportDto(
            filterParts.Count == 0 ? "All active items" : string.Join(" · ", filterParts),
            rows,
            rows.Count,
            rows.Sum(r => r.Quantity),
            rows.Sum(r => r.PurchasePrice ?? 0m),
            rows.Sum(r => r.EstimatedValue ?? 0m));
    }
}
