using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class AttachmentService(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICurrentUserService currentUser,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    public async Task<IReadOnlyList<AttachmentDto>> GetForItemAsync(int itemId, CancellationToken ct = default) =>
        await db.Attachments.AsNoTracking()
            .Where(a => a.ItemId == itemId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentDto(a.Id, a.ItemId, a.Type, a.OriginalFileName, a.ContentType, a.SizeBytes, a.Description, a.CreatedAt))
            .ToListAsync(ct);

    public async Task<AttachmentDto?> GetAsync(int attachmentId, CancellationToken ct = default) =>
        await db.Attachments.AsNoTracking()
            .Where(a => a.Id == attachmentId)
            .Select(a => new AttachmentDto(a.Id, a.ItemId, a.Type, a.OriginalFileName, a.ContentType, a.SizeBytes, a.Description, a.CreatedAt))
            .FirstOrDefaultAsync(ct);

    public async Task<Result<int>> AddAsync(int itemId, Stream content, string originalFileName, string contentType,
        AttachmentType type, string? description, CancellationToken ct = default)
    {
        if (!await db.Items.AnyAsync(i => i.Id == itemId, ct))
            return Result<int>.Failure("Item not found.");

        string storedPath;
        try
        {
            storedPath = await storage.SaveAsync(content, originalFileName, contentType, ct);
        }
        catch (InvalidOperationException ex)
        {
            // Validation failures (type/size) surface as friendly errors, not exceptions.
            return Result<int>.Failure(ex.Message);
        }

        var full = storage.ResolvePhysicalPath(storedPath);
        var size = new FileInfo(full).Length;

        var entity = new ItemAttachment
        {
            ItemId = itemId,
            Type = type,
            OriginalFileName = SanitizeName(originalFileName),
            StoredPath = storedPath,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = size,
            Description = description?.Trim()
        };
        db.Attachments.Add(entity);

        db.ItemHistory.Add(new ItemHistory
        {
            ItemId = itemId,
            Action = HistoryAction.AttachmentAdded,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Summary = $"Attachment added: {entity.OriginalFileName}",
            Timestamp = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Attachment {AttachmentId} added to item {ItemId} ({Size} bytes)", entity.Id, itemId, size);
        return Result<int>.Success(entity.Id);
    }

    public async Task<AttachmentContent?> OpenAsync(int attachmentId, CancellationToken ct = default)
    {
        var a = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId, ct);
        if (a is null) return null;
        var stream = await storage.OpenReadAsync(a.StoredPath, ct);
        return stream is null ? null : new AttachmentContent(stream, a.ContentType, a.OriginalFileName);
    }

    public async Task<Result> DeleteAsync(int attachmentId, CancellationToken ct = default)
    {
        var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == attachmentId, ct);
        if (a is null) return Result.Failure("Attachment not found.");

        db.Attachments.Remove(a);
        db.ItemHistory.Add(new ItemHistory
        {
            ItemId = a.ItemId,
            Action = HistoryAction.AttachmentRemoved,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Summary = $"Attachment removed: {a.OriginalFileName}",
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        // Best-effort binary cleanup after the metadata is gone.
        try { await storage.DeleteAsync(a.StoredPath, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete attachment binary {Path}", a.StoredPath); }

        return Result.Success();
    }

    private static string SanitizeName(string name)
    {
        var justName = Path.GetFileName(name);
        return string.IsNullOrWhiteSpace(justName) ? "file" : justName;
    }
}
