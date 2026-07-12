using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class ProjectService(
    IApplicationDbContext db,
    IInventoryLedgerService ledger,
    ICurrentUserService currentUser,
    ILogger<ProjectService> logger) : IProjectService
{
    public async Task<IReadOnlyList<ProjectListDto>> GetAllAsync(bool includeClosed = true, CancellationToken ct = default)
    {
        var query = db.Projects.AsNoTracking();
        if (!includeClosed) query = query.Where(p => p.Status == ProjectStatus.Open);
        return await query
            .OrderByDescending(p => p.Status == ProjectStatus.Open)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new ProjectListDto(p.Id, p.Name, p.Status, p.Allocations.Count, p.CreatedAt, p.CompletedAt))
            .ToListAsync(ct);
    }

    public async Task<ProjectDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return null;

        var allocations = await db.ProjectAllocations.AsNoTracking()
            .Where(a => a.ProjectId == id)
            .OrderBy(a => a.Item.Name)
            .Select(a => new ProjectAllocationDto(
                a.Id, a.ItemId, a.Item.Name, a.Item.Unit, a.QuantityAllocated, a.QuantityConsumed, a.Consumable,
                a.Item.Location != null ? a.Item.Location.Name : null, a.Item.Container))
            .ToListAsync(ct);

        return new ProjectDto(project.Id, project.Name, project.Description, project.Status,
            project.CreatedAt, project.CompletedAt, allocations);
    }

    public async Task<Result<int>> CreateAsync(ProjectEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result<int>.Failure(errors);
        var project = new Project
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            Status = ProjectStatus.Open,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Project {ProjectId} '{Name}' created", project.Id, project.Name);
        return Result<int>.Success(project.Id);
    }

    public async Task<Result> UpdateAsync(int id, ProjectEditModel model, CancellationToken ct = default)
    {
        if (!ModelValidator.TryValidate(model, out var errors)) return Result.Failure(errors);
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return Result.Failure("Project not found.");
        project.Name = model.Name.Trim();
        project.Description = model.Description?.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> AddAllocationAsync(int projectId, int itemId, decimal quantity, bool consumable, string? note, CancellationToken ct = default)
    {
        if (quantity <= 0) return Result.Failure("Enter an amount greater than zero.");
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Result.Failure("Project not found.");
        if (project.Status != ProjectStatus.Open) return Result.Failure("This project is closed.");

        // If the item is already allocated, adjust its quantity instead of duplicating.
        var existing = await db.ProjectAllocations.FirstOrDefaultAsync(a => a.ProjectId == projectId && a.ItemId == itemId, ct);
        if (existing is not null)
            return await SetAllocationQuantityAsync(existing.Id, existing.QuantityAllocated + quantity, ct);

        // Reserve the stock via the ledger (fails if not enough is available).
        var checkout = await ledger.CheckOutAsync(itemId, quantity, ProjectNote(project.Name, note), projectId, ct);
        if (!checkout.Succeeded) return checkout;

        db.ProjectAllocations.Add(new ProjectAllocation
        {
            ProjectId = projectId,
            ItemId = itemId,
            QuantityAllocated = quantity,
            Consumable = consumable,
            Note = note?.Trim()
        });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetAllocationQuantityAsync(int allocationId, decimal newQuantity, CancellationToken ct = default)
    {
        if (newQuantity < 0) return Result.Failure("Amount cannot be negative.");
        var alloc = await db.ProjectAllocations.Include(a => a.Project).FirstOrDefaultAsync(a => a.Id == allocationId, ct);
        if (alloc is null) return Result.Failure("Allocation not found.");
        if (alloc.Project.Status != ProjectStatus.Open) return Result.Failure("This project is closed.");

        var delta = newQuantity - alloc.QuantityAllocated;
        if (delta > 0)
        {
            var r = await ledger.CheckOutAsync(alloc.ItemId, delta, ProjectNote(alloc.Project.Name, alloc.Note), alloc.ProjectId, ct);
            if (!r.Succeeded) return r;
        }
        else if (delta < 0)
        {
            var r = await ledger.ReturnAsync(alloc.ItemId, -delta, ProjectNote(alloc.Project.Name, alloc.Note), alloc.ProjectId, ct);
            if (!r.Succeeded) return r;
        }

        if (newQuantity == 0)
            db.ProjectAllocations.Remove(alloc);
        else
            alloc.QuantityAllocated = newQuantity;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveAllocationAsync(int allocationId, CancellationToken ct = default)
    {
        var alloc = await db.ProjectAllocations.Include(a => a.Project).FirstOrDefaultAsync(a => a.Id == allocationId, ct);
        if (alloc is null) return Result.Failure("Allocation not found.");
        if (alloc.Project.Status != ProjectStatus.Open) return Result.Failure("This project is closed.");

        if (alloc.QuantityAllocated > 0)
        {
            var r = await ledger.ReturnAsync(alloc.ItemId, alloc.QuantityAllocated, ProjectNote(alloc.Project.Name, alloc.Note), alloc.ProjectId, ct);
            if (!r.Succeeded) return r;
        }
        db.ProjectAllocations.Remove(alloc);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> CompleteAsync(int projectId, IReadOnlyList<CloseoutLine> lines, CancellationToken ct = default)
    {
        var project = await db.Projects.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Result.Failure("Project not found.");
        if (project.Status != ProjectStatus.Open) return Result.Failure("This project is already closed.");

        var usedByAlloc = lines.ToDictionary(l => l.AllocationId, l => l.UsedAmount);

        foreach (var alloc in project.Allocations)
        {
            // Default: consumables used fully, everything else returned.
            var used = usedByAlloc.TryGetValue(alloc.Id, out var u) ? u : (alloc.Consumable ? alloc.QuantityAllocated : 0m);
            used = Math.Clamp(used, 0, alloc.QuantityAllocated);
            var returned = alloc.QuantityAllocated - used;

            if (used > 0)
            {
                var r = await ledger.ConsumeAsync(alloc.ItemId, used, ProjectNote(project.Name, "used for project"), projectId, ct);
                if (!r.Succeeded) return Result.Failure($"Could not close out '{alloc.ItemId}': {string.Join(" ", r.Errors)}");
            }
            if (returned > 0)
            {
                var r = await ledger.ReturnAsync(alloc.ItemId, returned, ProjectNote(project.Name, "returned after project"), projectId, ct);
                if (!r.Succeeded) return Result.Failure(string.Join(" ", r.Errors));
            }
            alloc.QuantityConsumed = used;
        }

        project.Status = ProjectStatus.Completed;
        project.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Project {ProjectId} completed", projectId);
        return Result.Success();
    }

    public async Task<Result> CancelAsync(int projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Result.Failure("Project not found.");
        if (project.Status != ProjectStatus.Open) return Result.Failure("This project is already closed.");

        foreach (var alloc in project.Allocations.Where(a => a.QuantityAllocated > 0))
        {
            var r = await ledger.ReturnAsync(alloc.ItemId, alloc.QuantityAllocated, ProjectNote(project.Name, "project cancelled"), projectId, ct);
            if (!r.Succeeded) return Result.Failure(string.Join(" ", r.Errors));
        }
        project.Status = ProjectStatus.Cancelled;
        project.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Result.Failure("Project not found.");

        // If still open, return everything to inventory before deleting.
        if (project.Status == ProjectStatus.Open)
        {
            var cancel = await CancelAsync(projectId, ct);
            if (!cancel.Succeeded) return cancel;
        }
        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string ProjectNote(string projectName, string? note) =>
        string.IsNullOrWhiteSpace(note) ? $"Project: {projectName}" : $"Project: {projectName} — {note}";
}
