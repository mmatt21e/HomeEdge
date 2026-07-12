using System.Text.Json;
using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class BackupRestoreService(
    IApplicationDbContext db,
    ICodeGenerator codes,
    ILogger<BackupRestoreService> logger) : IBackupRestoreService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<BackupSummary>> ValidateAsync(Stream json, CancellationToken ct = default)
    {
        var parsed = await ParseAsync(json, ct);
        if (parsed is null) return Result<BackupSummary>.Failure("The file is not a valid HomeStock JSON backup.");

        var warnings = new List<string>();
        if (parsed.Schema != 1) warnings.Add($"Backup schema version {parsed.Schema} may not be fully compatible.");
        if (parsed.Items.Count == 0) warnings.Add("The backup contains no items.");

        var hasItems = await db.Items.AnyAsync(ct);
        if (hasItems) warnings.Add("This database already contains items; a restore will merge, not overwrite.");

        return Result<BackupSummary>.Success(new BackupSummary(
            parsed.Schema, parsed.ExportedAtUtc, parsed.Categories.Count, parsed.Locations.Count,
            parsed.Items.Count, parsed.Tags.Count, hasItems, warnings));
    }

    public async Task<Result<RestoreResult>> RestoreAsync(Stream json, CancellationToken ct = default)
    {
        var parsed = await ParseAsync(json, ct);
        if (parsed is null) return Result<RestoreResult>.Failure("The file is not a valid HomeStock JSON backup.");

        int catsAdded = 0, locsAdded = 0, itemsAdded = 0, itemsSkipped = 0;

        // ---- Categories (by name) ----
        var categoryIdByOld = new Dictionary<int, int>();
        var existingCats = await db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var c in parsed.Categories)
        {
            if (existingCats.TryGetValue(c.Name, out var id)) { categoryIdByOld[c.Id] = id; continue; }
            var entity = new Category { Name = c.Name, Description = c.Description, Color = c.Color, Icon = c.Icon };
            db.Categories.Add(entity);
            await db.SaveChangesAsync(ct);
            categoryIdByOld[c.Id] = entity.Id;
            existingCats[c.Name] = entity.Id;
            catsAdded++;
        }

        // ---- Locations (preserve hierarchy; reuse existing code, else keep/generate) ----
        var locationIdByOld = new Dictionary<int, int>();
        var existingByCode = await db.Locations.ToDictionaryAsync(l => l.Code, l => l.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existingCodes = new HashSet<string>(existingByCode.Keys, StringComparer.OrdinalIgnoreCase);

        // Process parents before children.
        foreach (var l in TopoSortLocations(parsed.Locations))
        {
            if (!string.IsNullOrWhiteSpace(l.Code) && existingByCode.TryGetValue(l.Code, out var existingId))
            {
                locationIdByOld[l.Id] = existingId;
                continue;
            }
            var code = !string.IsNullOrWhiteSpace(l.Code) && existingCodes.Add(l.Code) ? l.Code : UniqueCode(existingCodes);
            int? parentId = l.ParentId is int p && locationIdByOld.TryGetValue(p, out var np) ? np : null;
            var entity = new Location { Name = l.Name, Description = l.Description, Code = code, ParentId = parentId };
            db.Locations.Add(entity);
            await db.SaveChangesAsync(ct);
            locationIdByOld[l.Id] = entity.Id;
            locsAdded++;
        }

        // ---- Tags (by name) ----
        var tagByName = await db.Tags.ToDictionaryAsync(t => t.NormalizedName, t => t, ct);

        // ---- Items (skip existing by name+serial+barcode) ----
        foreach (var it in parsed.Items)
        {
            var exists = await db.Items.AnyAsync(x =>
                x.Name == it.Name && x.SerialNumber == it.SerialNumber && x.Barcode == it.Barcode, ct);
            if (exists) { itemsSkipped++; continue; }

            var entity = new InventoryItem
            {
                Name = it.Name, Description = it.Description, Notes = it.Notes,
                Subcategory = it.Subcategory, Quantity = it.Quantity <= 0 ? 1 : it.Quantity, Unit = it.Unit,
                Manufacturer = it.Manufacturer, Brand = it.Brand, ModelNumber = it.ModelNumber,
                SerialNumber = it.SerialNumber, Barcode = it.Barcode,
                PurchaseDate = it.PurchaseDate, PurchaseLocation = it.PurchaseLocation,
                PurchasePrice = it.PurchasePrice, EstimatedValue = it.EstimatedValue,
                Condition = Enum.TryParse<ItemCondition>(it.Condition, out var cond) ? cond : ItemCondition.Unknown,
                WarrantyExpiration = it.WarrantyExpiration, Container = it.Container,
                Status = Enum.TryParse<ItemStatus>(it.Status, out var st) ? st : ItemStatus.Available,
                IsArchived = it.IsArchived,
                CategoryId = it.CategoryId is int oc && categoryIdByOld.TryGetValue(oc, out var nc) ? nc : null,
                LocationId = it.LocationId is int ol && locationIdByOld.TryGetValue(ol, out var nl) ? nl : null,
            };

            foreach (var tagName in it.Tags ?? new List<string>())
            {
                var norm = tagName.Trim().ToLowerInvariant();
                if (norm.Length == 0) continue;
                if (!tagByName.TryGetValue(norm, out var tag))
                {
                    tag = new Tag { Name = tagName.Trim(), NormalizedName = norm };
                    db.Tags.Add(tag);
                    tagByName[norm] = tag;
                }
                entity.ItemTags.Add(new ItemTag { Tag = tag });
            }

            db.Items.Add(entity);
            db.ItemHistory.Add(new ItemHistory { Item = entity, Action = HistoryAction.Created, Summary = "Restored from backup", Timestamp = DateTime.UtcNow });
            itemsAdded++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Restore complete: +{Cats} categories, +{Locs} locations, +{Items} items ({Skip} skipped)",
            catsAdded, locsAdded, itemsAdded, itemsSkipped);
        return Result<RestoreResult>.Success(new RestoreResult(catsAdded, locsAdded, itemsAdded, itemsSkipped));
    }

    private string UniqueCode(HashSet<string> used)
    {
        for (var i = 0; i < 20; i++)
        {
            var code = codes.NewLocationCode();
            if (used.Add(code)) return code;
        }
        throw new InvalidOperationException("Unable to generate a unique location code during restore.");
    }

    private static IEnumerable<BackupLocation> TopoSortLocations(List<BackupLocation> locations)
    {
        var byId = locations.ToDictionary(l => l.Id);
        var visited = new HashSet<int>();
        var ordered = new List<BackupLocation>();
        void Visit(BackupLocation l)
        {
            if (!visited.Add(l.Id)) return;
            if (l.ParentId is int p && byId.TryGetValue(p, out var parent)) Visit(parent);
            ordered.Add(l);
        }
        foreach (var l in locations) Visit(l);
        return ordered;
    }

    private static async Task<BackupRoot?> ParseAsync(Stream json, CancellationToken ct)
    {
        try
        {
            if (json.CanSeek) json.Position = 0;
            var root = await JsonSerializer.DeserializeAsync<BackupRoot>(json, JsonOpts, ct);
            return root is { Items: not null } ? root : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---- Backup JSON shape (mirrors ImportExportService.ExportJsonBackupAsync) ----
    private sealed class BackupRoot
    {
        public DateTime? ExportedAtUtc { get; set; }
        public int Schema { get; set; }
        public List<BackupCategory> Categories { get; set; } = new();
        public List<BackupLocation> Locations { get; set; } = new();
        public List<BackupTag> Tags { get; set; } = new();
        public List<BackupItem> Items { get; set; } = new();
    }
    private sealed class BackupCategory { public int Id { get; set; } public string Name { get; set; } = ""; public string? Description { get; set; } public string? Color { get; set; } public string? Icon { get; set; } }
    private sealed class BackupLocation { public int Id { get; set; } public string Name { get; set; } = ""; public string? Description { get; set; } public int? ParentId { get; set; } public string Code { get; set; } = ""; }
    private sealed class BackupTag { public int Id { get; set; } public string Name { get; set; } = ""; }
    private sealed class BackupItem
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public int? CategoryId { get; set; }
        public string? Subcategory { get; set; }
        public decimal Quantity { get; set; } = 1;
        public string? Unit { get; set; }
        public string? Manufacturer { get; set; }
        public string? Brand { get; set; }
        public string? ModelNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string? Barcode { get; set; }
        public DateOnly? PurchaseDate { get; set; }
        public string? PurchaseLocation { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? EstimatedValue { get; set; }
        public string? Condition { get; set; }
        public DateOnly? WarrantyExpiration { get; set; }
        public int? LocationId { get; set; }
        public string? Container { get; set; }
        public string? Status { get; set; }
        public bool IsArchived { get; set; }
        public List<string>? Tags { get; set; }
    }
}
