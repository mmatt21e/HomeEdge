using HomeStock.Domain.Enums;

namespace HomeStock.Application.Models;

public record AttachmentDto(
    int Id,
    int ItemId,
    AttachmentType Type,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? Description,
    DateTime CreatedAt)
{
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:0.#} KB",
        _ => $"{SizeBytes / (1024.0 * 1024.0):0.#} MB"
    };
}

/// <summary>An opened attachment ready to stream back to the caller.</summary>
public record AttachmentContent(Stream Content, string ContentType, string FileName);
