using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class CategoryService(IApplicationDbContext db, ILogger<CategoryService> logger) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var query = db.Categories.AsNoTracking();
        if (!includeArchived) query = query.Where(c => !c.IsArchived);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Color, c.Icon, c.IsSystem,
                c.IsArchived, c.Items.Count(i => !i.IsArchived)))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto?> GetAsync(int id, CancellationToken ct = default) =>
        await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Color, c.Icon, c.IsSystem,
                c.IsArchived, c.Items.Count(i => !i.IsArchived)))
            .FirstOrDefaultAsync(ct);

    public async Task<Result<int>> CreateAsync(CategoryEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result<int>.Failure(errors);

        var name = model.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name, ct))
            return Result<int>.Failure($"A category named '{name}' already exists.");

        var entity = new Category
        {
            Name = name,
            Description = model.Description?.Trim(),
            Color = model.Color,
            Icon = model.Icon
        };
        db.Categories.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Category {CategoryId} '{Name}' created", entity.Id, entity.Name);
        return Result<int>.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(int id, CategoryEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result.Failure(errors);

        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return Result.Failure("Category not found.");

        var name = model.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != id, ct))
            return Result.Failure($"A category named '{name}' already exists.");

        entity.Name = name;
        entity.Description = model.Description?.Trim();
        entity.Color = model.Color;
        entity.Icon = model.Icon;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ArchiveAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return Result.Failure("Category not found.");
        entity.IsArchived = true;
        entity.ArchivedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return Result.Failure("Category not found.");
        entity.IsArchived = false;
        entity.ArchivedAt = null;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return Result.Failure("Category not found.");
        if (entity.IsSystem) return Result.Failure("System categories cannot be deleted. Archive it instead.");

        var inUse = await db.Items.CountAsync(i => i.CategoryId == id, ct);
        if (inUse > 0)
            return Result.Failure($"Cannot delete: {inUse} item(s) still use this category. Reassign or archive instead.");

        db.Categories.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
