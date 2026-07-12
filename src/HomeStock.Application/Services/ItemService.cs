using System.Text.Json;
using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class ItemService(
    IApplicationDbContext db,
    ILocationService locations,
    ICurrentUserService currentUser,
    ILogger<ItemService> logger) : IItemService
{
    /// <summary>Warranties expiring within this many days count as "expiring soon".</summary>
    public const int WarrantyWarningDays = 30;

    public async Task<PagedResult<ItemListDto>> SearchAsync(ItemQuery q, CancellationToken ct = default)
    {
        var query = db.Items.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.Location)
            .AsQueryable();

        // Archived scope
        if (q.OnlyArchived) query = query.Where(i => i.IsArchived);
        else if (!q.IncludeArchived) query = query.Where(i => !i.IsArchived);

        // Full-text-ish, case-insensitive search across the key fields. Comparing on lower-cased
        // values keeps the match case-insensitive across providers (SQLite translates Contains to
        // the case-sensitive instr()), which matches user expectations.
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(i =>
                i.Name.ToLower().Contains(s) ||
                (i.Description != null && i.Description.ToLower().Contains(s)) ||
                (i.Manufacturer != null && i.Manufacturer.ToLower().Contains(s)) ||
                (i.Brand != null && i.Brand.ToLower().Contains(s)) ||
                (i.ModelNumber != null && i.ModelNumber.ToLower().Contains(s)) ||
                (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(s)) ||
                (i.Barcode != null && i.Barcode.ToLower().Contains(s)) ||
                (i.Notes != null && i.Notes.ToLower().Contains(s)) ||
                (i.Category != null && i.Category.Name.ToLower().Contains(s)) ||
                (i.Location != null && i.Location.Name.ToLower().Contains(s)) ||
                i.ItemTags.Any(t => t.Tag.Name.ToLower().Contains(s)));
        }

        if (q.CategoryId is int cat) query = query.Where(i => i.CategoryId == cat);

        if (q.LocationId is int loc)
        {
            if (q.IncludeSublocations)
            {
                var ids = await locations.GetSelfAndDescendantIdsAsync(loc, ct);
                query = query.Where(i => i.LocationId != null && ids.Contains(i.LocationId.Value));
            }
            else query = query.Where(i => i.LocationId == loc);
        }

        if (q.Condition is ItemCondition cond) query = query.Where(i => i.Condition == cond);
        if (q.Status is ItemStatus st) query = query.Where(i => i.Status == st);
        if (q.OnlyLoaned) query = query.Where(i => i.Status == ItemStatus.Loaned);
        if (q.MissingLocation) query = query.Where(i => i.LocationId == null);
        if (q.MissingPhoto) query = query.Where(i => !i.Attachments.Any(a => a.Type == AttachmentType.Photo));
        if (q.PurchasedAfter is DateOnly pa) query = query.Where(i => i.PurchaseDate >= pa);
        if (q.PurchasedBefore is DateOnly pb) query = query.Where(i => i.PurchaseDate <= pb);
        if (q.MinValue is decimal mn) query = query.Where(i => i.EstimatedValue >= mn);
        if (q.MaxValue is decimal mx) query = query.Where(i => i.EstimatedValue <= mx);
        if (!string.IsNullOrWhiteSpace(q.Tag))
        {
            var tag = q.Tag.Trim();
            query = query.Where(i => i.ItemTags.Any(t => t.Tag.Name == tag));
        }

        if (q.Warranty is WarrantyState ws && ws != WarrantyState.None)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var soon = today.AddDays(WarrantyWarningDays);
            query = ws switch
            {
                WarrantyState.Expired => query.Where(i => i.WarrantyExpiration != null && i.WarrantyExpiration < today),
                WarrantyState.ExpiringSoon => query.Where(i => i.WarrantyExpiration != null && i.WarrantyExpiration >= today && i.WarrantyExpiration <= soon),
                WarrantyState.Active => query.Where(i => i.WarrantyExpiration != null && i.WarrantyExpiration > soon),
                _ => query
            };
        }

        // Sorting
        query = (q.SortBy, q.SortDescending) switch
        {
            ("name", false) => query.OrderBy(i => i.Name),
            ("name", true) => query.OrderByDescending(i => i.Name),
            ("value", false) => query.OrderBy(i => i.EstimatedValue),
            ("value", true) => query.OrderByDescending(i => i.EstimatedValue),
            ("created", false) => query.OrderBy(i => i.CreatedAt),
            ("created", true) => query.OrderByDescending(i => i.CreatedAt),
            (_, false) => query.OrderBy(i => i.UpdatedAt),
            _ => query.OrderByDescending(i => i.UpdatedAt),
        };

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var items = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new ItemListDto(
                i.Id, i.Name, i.Category != null ? i.Category.Name : null,
                i.Location != null ? i.Location.Name : null, i.Quantity, i.Status, i.Condition,
                i.EstimatedValue, i.Barcode, i.IsArchived,
                i.Attachments.Any(a => a.Type == AttachmentType.Photo), i.WarrantyExpiration))
            .ToListAsync(ct);

        return new PagedResult<ItemListDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<ItemDto?> GetAsync(int id, CancellationToken ct = default) =>
        await db.Items.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(ProjectToDto)
            .FirstOrDefaultAsync(ct);

    public async Task<ItemDto?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        barcode = barcode.Trim();
        return await db.Items.AsNoTracking()
            .Where(i => i.Barcode == barcode && !i.IsArchived)
            .Select(ProjectToDto)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<string>> CheckDuplicatesAsync(string? serial, string? barcode, int? excludeItemId, CancellationToken ct = default)
    {
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(serial))
        {
            var s = serial.Trim();
            var dup = await db.Items.Where(i => i.SerialNumber == s && i.Id != excludeItemId)
                .Select(i => i.Name).FirstOrDefaultAsync(ct);
            if (dup is not null) warnings.Add($"Serial number '{s}' is already used by item '{dup}'.");
        }
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var b = barcode.Trim();
            var dup = await db.Items.Where(i => i.Barcode == b && i.Id != excludeItemId)
                .Select(i => i.Name).FirstOrDefaultAsync(ct);
            if (dup is not null) warnings.Add($"Barcode '{b}' is already used by item '{dup}'.");
        }
        return warnings;
    }

    public async Task<Result<int>> CreateAsync(ItemEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result<int>.Failure(errors);
        var validation = await ValidateReferencesAsync(model, ct);
        if (validation.Count > 0) return Result<int>.Failure(validation);

        var entity = new InventoryItem();
        Apply(model, entity);
        await SyncTagsAsync(entity, model.Tags, ct);

        db.Items.Add(entity);
        await db.SaveChangesAsync(ct);

        AddHistory(entity.Id, HistoryAction.Created, $"Item '{entity.Name}' created", null);
        await db.SaveChangesAsync(ct);

        var warnings = await CheckDuplicatesAsync(model.SerialNumber, model.Barcode, entity.Id, ct);
        logger.LogInformation("Item {ItemId} '{Name}' created by {User}", entity.Id, entity.Name, currentUser.UserName ?? "system");
        return Result<int>.Success(entity.Id, warnings);
    }

    public async Task<Result> UpdateAsync(int id, ItemEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result.Failure(errors);
        var validation = await ValidateReferencesAsync(model, ct);
        if (validation.Count > 0) return Result.Failure(validation);

        var entity = await db.Items.Include(i => i.ItemTags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (entity is null) return Result.Failure("Item not found.");

        var before = Snapshot(entity);
        var oldStatus = entity.Status;
        Apply(model, entity);
        await SyncTagsAsync(entity, model.Tags, ct);
        var changes = Diff(before, Snapshot(entity));

        if (changes.Count > 0)
        {
            var summary = entity.Status != oldStatus
                ? $"Status: {oldStatus} → {entity.Status}"
                : $"{changes.Count} field(s) updated";
            var action = entity.Status != oldStatus ? HistoryAction.StatusChanged : HistoryAction.Updated;
            AddHistory(entity.Id, action, summary, changes);
        }

        await db.SaveChangesAsync(ct);
        var warnings = await CheckDuplicatesAsync(model.SerialNumber, model.Barcode, entity.Id, ct);
        return Result.Success(warnings);
    }

    public async Task<Result> ArchiveAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (entity is null) return Result.Failure("Item not found.");
        entity.IsArchived = true;
        entity.ArchivedAt = DateTime.UtcNow;
        AddHistory(entity.Id, HistoryAction.Archived, "Item archived", null);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (entity is null) return Result.Failure("Item not found.");
        entity.IsArchived = false;
        entity.ArchivedAt = null;
        AddHistory(entity.Id, HistoryAction.Restored, "Item restored", null);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (entity is null) return Result.Failure("Item not found.");
        db.Items.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Item {ItemId} '{Name}' hard-deleted by {User}", id, entity.Name, currentUser.UserName ?? "system");
        return Result.Success();
    }

    public async Task<Result> AssignBarcodeAsync(int itemId, string barcode, CancellationToken ct = default)
    {
        barcode = barcode.Trim();
        if (string.IsNullOrEmpty(barcode)) return Result.Failure("Barcode is empty.");

        var entity = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (entity is null) return Result.Failure("Item not found.");

        var warnings = await CheckDuplicatesAsync(null, barcode, itemId, ct);
        entity.Barcode = barcode;
        AddHistory(entity.Id, HistoryAction.Updated, $"Barcode assigned: {barcode}", null);
        await db.SaveChangesAsync(ct);
        return Result.Success(warnings);
    }

    // --- helpers ---

    private async Task<IReadOnlyList<string>> ValidateReferencesAsync(ItemEditModel m, CancellationToken ct)
    {
        var errors = new List<string>();
        if (m.CategoryId is int c && !await db.Categories.AnyAsync(x => x.Id == c, ct))
            errors.Add("Selected category no longer exists.");
        if (m.LocationId is int l && !await db.Locations.AnyAsync(x => x.Id == l, ct))
            errors.Add("Selected location no longer exists.");
        return errors;
    }

    private static void Apply(ItemEditModel m, InventoryItem e)
    {
        e.Name = m.Name.Trim();
        e.Description = m.Description?.Trim();
        e.Notes = m.Notes?.Trim();
        e.CategoryId = m.CategoryId;
        e.Subcategory = m.Subcategory?.Trim();
        e.Quantity = m.Quantity;
        e.Manufacturer = m.Manufacturer?.Trim();
        e.Brand = m.Brand?.Trim();
        e.ModelNumber = m.ModelNumber?.Trim();
        e.SerialNumber = m.SerialNumber?.Trim();
        e.Barcode = m.Barcode?.Trim();
        e.PurchaseDate = m.PurchaseDate;
        e.PurchaseLocation = m.PurchaseLocation?.Trim();
        e.PurchasePrice = m.PurchasePrice;
        e.EstimatedValue = m.EstimatedValue;
        e.Condition = m.Condition;
        e.WarrantyExpiration = m.WarrantyExpiration;
        e.LocationId = m.LocationId;
        e.Container = m.Container?.Trim();
        e.Status = m.Status;
    }

    private async Task SyncTagsAsync(InventoryItem entity, List<string> tagNames, CancellationToken ct)
    {
        var wanted = tagNames
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .GroupBy(t => t.ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        entity.ItemTags.Clear();
        foreach (var name in wanted)
        {
            var normalized = name.ToLowerInvariant();
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == normalized, ct)
                      ?? new Tag { Name = name, NormalizedName = normalized };
            if (tag.Id == 0) db.Tags.Add(tag);
            entity.ItemTags.Add(new ItemTag { Tag = tag });
        }
    }

    private void AddHistory(int itemId, HistoryAction action, string summary, Dictionary<string, object?>? changes)
    {
        db.ItemHistory.Add(new ItemHistory
        {
            ItemId = itemId,
            Action = action,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Summary = summary,
            ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes),
            Timestamp = DateTime.UtcNow
        });
    }

    private static Dictionary<string, object?> Snapshot(InventoryItem e) => new()
    {
        [nameof(e.Name)] = e.Name,
        [nameof(e.Description)] = e.Description,
        [nameof(e.CategoryId)] = e.CategoryId,
        [nameof(e.Quantity)] = e.Quantity,
        [nameof(e.SerialNumber)] = e.SerialNumber,
        [nameof(e.Barcode)] = e.Barcode,
        [nameof(e.EstimatedValue)] = e.EstimatedValue,
        [nameof(e.Condition)] = e.Condition.ToString(),
        [nameof(e.Status)] = e.Status.ToString(),
        [nameof(e.LocationId)] = e.LocationId,
        [nameof(e.Container)] = e.Container,
    };

    private static Dictionary<string, object?> Diff(Dictionary<string, object?> before, Dictionary<string, object?> after)
    {
        var changes = new Dictionary<string, object?>();
        foreach (var (key, oldVal) in before)
        {
            var newVal = after[key];
            if (!Equals(oldVal, newVal))
                changes[key] = new { old = oldVal, @new = newVal };
        }
        return changes;
    }

    private static readonly System.Linq.Expressions.Expression<Func<InventoryItem, ItemDto>> ProjectToDto =
        i => new ItemDto(
            i.Id, i.Name, i.Description, i.Notes, i.CategoryId,
            i.Category != null ? i.Category.Name : null, i.Subcategory, i.Quantity,
            i.Manufacturer, i.Brand, i.ModelNumber, i.SerialNumber, i.Barcode,
            i.PurchaseDate, i.PurchaseLocation, i.PurchasePrice, i.EstimatedValue, i.Condition,
            i.WarrantyExpiration, i.LocationId, i.Location != null ? i.Location.Name : null, i.Container,
            i.Status, i.IsArchived, i.CreatedAt, i.UpdatedAt,
            i.ItemTags.Select(t => t.Tag.Name).ToList(),
            i.Attachments.Count);
}
