using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class AuditService(
    IApplicationDbContext db,
    ILocationService locations,
    ICurrentUserService currentUser,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task<IReadOnlyList<AuditListDto>> GetHistoryAsync(CancellationToken ct = default) =>
        await db.Audits.AsNoTracking()
            .OrderByDescending(a => a.StartedAt)
            .Select(a => new AuditListDto(
                a.Id,
                a.Location.Name,
                a.Status,
                a.StartedAt,
                a.CompletedAt,
                a.Items.Count,
                a.Items.Count(i => i.Result == AuditItemResult.Missing
                    || i.Result == AuditItemResult.Moved
                    || i.Result == AuditItemResult.Damaged)))
            .ToListAsync(ct);

    public async Task<AuditDto?> GetAsync(int auditId, CancellationToken ct = default)
    {
        var audit = await db.Audits.AsNoTracking()
            .Include(a => a.Location)
            .FirstOrDefaultAsync(a => a.Id == auditId, ct);
        if (audit is null) return null;

        var items = await db.AuditItems.AsNoTracking()
            .Where(ai => ai.AuditId == auditId)
            .OrderBy(ai => ai.Item.Name)
            .Select(ai => new AuditItemDto(
                ai.Id, ai.AuditId, ai.ItemId, ai.Item.Name, ai.Item.Barcode,
                ai.Item.Location != null ? ai.Item.Location.Name : null,
                ai.Result, ai.MovedToLocationId,
                ai.MovedToLocation != null ? ai.MovedToLocation.Name : null, ai.Notes))
            .ToListAsync(ct);

        return new AuditDto(audit.Id, audit.LocationId, audit.Location.Name, audit.Status,
            audit.IncludeSublocations, audit.StartedAt, audit.CompletedAt, audit.Notes, items);
    }

    public async Task<Result<int>> StartAsync(int locationId, bool includeSublocations, CancellationToken ct = default)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null) return Result<int>.Failure("Location not found.");

        var locationIds = includeSublocations
            ? await locations.GetSelfAndDescendantIdsAsync(locationId, ct)
            : new[] { locationId };

        var expectedItemIds = await db.Items
            .Where(i => !i.IsArchived && i.LocationId != null && locationIds.Contains(i.LocationId.Value))
            .Select(i => i.Id)
            .ToListAsync(ct);

        var audit = new InventoryAudit
        {
            LocationId = locationId,
            IncludeSublocations = includeSublocations,
            Status = AuditStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            PerformedByUserId = currentUser.UserId,
            Items = expectedItemIds.Select(id => new InventoryAuditItem { ItemId = id, Result = AuditItemResult.Pending }).ToList()
        };
        db.Audits.Add(audit);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Audit {AuditId} started for location {LocationId} ({Count} expected items)", audit.Id, locationId, expectedItemIds.Count);
        return Result<int>.Success(audit.Id);
    }

    public async Task<Result> RecordResultAsync(int auditItemId, AuditItemResult result, int? movedToLocationId, string? notes, CancellationToken ct = default)
    {
        var row = await db.AuditItems.Include(ai => ai.Audit).FirstOrDefaultAsync(ai => ai.Id == auditItemId, ct);
        if (row is null) return Result.Failure("Audit item not found.");
        if (row.Audit.Status != AuditStatus.InProgress) return Result.Failure("This audit is no longer in progress.");

        if (result == AuditItemResult.Moved && movedToLocationId is int loc && !await db.Locations.AnyAsync(l => l.Id == loc, ct))
            return Result.Failure("Selected 'moved to' location not found.");

        row.Result = result;
        row.MovedToLocationId = result == AuditItemResult.Moved ? movedToLocationId : null;
        row.Notes = notes?.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<int>> ConfirmByScanAsync(int auditId, string code, CancellationToken ct = default)
    {
        code = code.Trim();
        var audit = await db.Audits.FirstOrDefaultAsync(a => a.Id == auditId, ct);
        if (audit is null) return Result<int>.Failure("Audit not found.");
        if (audit.Status != AuditStatus.InProgress) return Result<int>.Failure("This audit is no longer in progress.");

        var row = await db.AuditItems
            .Include(ai => ai.Item)
            .FirstOrDefaultAsync(ai => ai.AuditId == auditId &&
                (ai.Item.Barcode == code || ai.Item.Name == code), ct);
        if (row is null) return Result<int>.Failure($"No expected item in this audit matches '{code}'.");

        row.Result = AuditItemResult.Confirmed;
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(row.Id);
    }

    public async Task<Result> CompleteAsync(int auditId, string? notes, CancellationToken ct = default)
    {
        var audit = await db.Audits.Include(a => a.Items).FirstOrDefaultAsync(a => a.Id == auditId, ct);
        if (audit is null) return Result.Failure("Audit not found.");
        if (audit.Status != AuditStatus.InProgress) return Result.Failure("This audit is already finished.");

        // Apply discrepancy outcomes to the underlying items, with history.
        foreach (var row in audit.Items)
        {
            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == row.ItemId, ct);
            if (item is null) continue;

            switch (row.Result)
            {
                case AuditItemResult.Missing:
                    item.Status = ItemStatus.Missing;
                    AddHistory(item.Id, HistoryAction.StatusChanged, "Marked missing during audit");
                    break;
                case AuditItemResult.Damaged:
                    item.Status = ItemStatus.Damaged;
                    AddHistory(item.Id, HistoryAction.StatusChanged, "Marked damaged during audit");
                    break;
                case AuditItemResult.Moved when row.MovedToLocationId is int newLoc:
                    item.LocationId = newLoc;
                    AddHistory(item.Id, HistoryAction.Moved, "Location updated during audit");
                    break;
            }
        }

        audit.Status = AuditStatus.Completed;
        audit.CompletedAt = DateTime.UtcNow;
        audit.Notes = notes?.Trim();
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Audit {AuditId} completed", auditId);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(int auditId, CancellationToken ct = default)
    {
        var audit = await db.Audits.FirstOrDefaultAsync(a => a.Id == auditId, ct);
        if (audit is null) return Result.Failure("Audit not found.");
        if (audit.Status != AuditStatus.InProgress) return Result.Failure("Only an in-progress audit can be cancelled.");
        audit.Status = AuditStatus.Cancelled;
        audit.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private void AddHistory(int itemId, HistoryAction action, string summary) =>
        db.ItemHistory.Add(new ItemHistory
        {
            ItemId = itemId,
            Action = action,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Summary = summary,
            Timestamp = DateTime.UtcNow
        });
}
