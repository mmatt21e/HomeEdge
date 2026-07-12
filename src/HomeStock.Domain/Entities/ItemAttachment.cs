using HomeStock.Domain.Common;
using HomeStock.Domain.Enums;

namespace HomeStock.Domain.Entities;

/// <summary>
/// Metadata for a file attached to an item (photo, receipt, warranty, manual, ...).
/// The binary is stored on disk outside the database under a safe generated name;
/// only the relative <see cref="StoredPath"/> and metadata live in the database.
/// </summary>
public class ItemAttachment : BaseEntity
{
    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;

    public AttachmentType Type { get; set; } = AttachmentType.Photo;

    /// <summary>Original filename as uploaded (for display only; never used on disk).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Safe, generated relative path under the attachment root (e.g. "ab/cd/uuid.jpg").</summary>
    public string StoredPath { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    public string? Description { get; set; }
}
