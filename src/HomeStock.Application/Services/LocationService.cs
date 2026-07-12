using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class LocationService(
    IApplicationDbContext db,
    ICodeGenerator codes,
    ILogger<LocationService> logger) : ILocationService
{
    private static LocationDto ToDto(Location l) =>
        new(l.Id, l.Name, l.Description, l.ParentId, l.Code, l.PhotoPath, l.IsArchived,
            l.Items.Count(i => !i.IsArchived));

    public async Task<IReadOnlyList<LocationDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var query = db.Locations.AsNoTracking().Include(l => l.Items);
        var list = await query.OrderBy(l => l.Name).ToListAsync(ct);
        return list.Where(l => includeArchived || !l.IsArchived).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<LocationTreeNode>> GetTreeAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var all = await db.Locations.AsNoTracking().Include(l => l.Items)
            .Where(l => includeArchived || !l.IsArchived)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

        var byParent = all.ToLookup(l => l.ParentId);
        var result = new List<LocationTreeNode>();

        void Walk(int? parentId, int depth, string prefix)
        {
            foreach (var node in byParent[parentId].OrderBy(l => l.Name))
            {
                var path = string.IsNullOrEmpty(prefix) ? node.Name : $"{prefix} / {node.Name}";
                result.Add(new LocationTreeNode(ToDto(node), depth) { Path = path });
                Walk(node.Id, depth + 1, path);
            }
        }
        Walk(null, 0, string.Empty);
        return result;
    }

    public async Task<LocationDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var l = await db.Locations.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        return l is null ? null : ToDto(l);
    }

    public async Task<LocationDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        code = code.Trim();
        var l = await db.Locations.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Code == code, ct);
        return l is null ? null : ToDto(l);
    }

    public async Task<IReadOnlyList<int>> GetSelfAndDescendantIdsAsync(int id, CancellationToken ct = default)
    {
        var edges = await db.Locations.AsNoTracking()
            .Select(l => new { l.Id, l.ParentId }).ToListAsync(ct);
        var byParent = edges.ToLookup(e => e.ParentId);
        var ids = new List<int>();
        var stack = new Stack<int>();
        stack.Push(id);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            ids.Add(cur);
            foreach (var child in byParent[cur]) stack.Push(child.Id);
        }
        return ids;
    }

    public async Task<Result<int>> CreateAsync(LocationEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result<int>.Failure(errors);
        if (model.ParentId is int p && !await db.Locations.AnyAsync(l => l.Id == p, ct))
            return Result<int>.Failure("Selected parent location no longer exists.");

        var entity = new Location
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            ParentId = model.ParentId,
            Code = await GenerateUniqueCodeAsync(ct)
        };
        db.Locations.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Location {LocationId} '{Name}' created (code {Code})", entity.Id, entity.Name, entity.Code);
        return Result<int>.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(int id, LocationEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result.Failure(errors);
        var entity = await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (entity is null) return Result.Failure("Location not found.");
        entity.Name = model.Name.Trim();
        entity.Description = model.Description?.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> MoveAsync(int id, int? newParentId, CancellationToken ct = default)
    {
        var entity = await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (entity is null) return Result.Failure("Location not found.");

        if (newParentId is int target)
        {
            if (target == id) return Result.Failure("A location cannot be its own parent.");
            var descendants = await GetSelfAndDescendantIdsAsync(id, ct);
            if (descendants.Contains(target))
                return Result.Failure("Cannot move a location into one of its own descendants.");
            if (!await db.Locations.AnyAsync(l => l.Id == target, ct))
                return Result.Failure("Target parent location not found.");
        }

        entity.ParentId = newParentId;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ArchiveAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (entity is null) return Result.Failure("Location not found.");
        entity.IsArchived = true;
        entity.ArchivedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (entity is null) return Result.Failure("Location not found.");
        entity.IsArchived = false;
        entity.ArchivedAt = null;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (entity is null) return Result.Failure("Location not found.");
        if (await db.Locations.AnyAsync(l => l.ParentId == id, ct))
            return Result.Failure("Cannot delete a location that has sub-locations. Move or delete them first.");
        if (await db.Items.AnyAsync(i => i.LocationId == id, ct))
            return Result.Failure("Cannot delete a location that still contains items. Reassign them first.");
        db.Locations.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = codes.NewLocationCode();
            if (!await db.Locations.AnyAsync(l => l.Code == code, ct)) return code;
        }
        throw new InvalidOperationException("Unable to generate a unique location code.");
    }
}
