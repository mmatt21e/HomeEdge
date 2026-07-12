using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Enums;

namespace HomeStock.Application.Abstractions;

public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentDto>> GetForItemAsync(int itemId, CancellationToken ct = default);

    Task<AttachmentDto?> GetAsync(int attachmentId, CancellationToken ct = default);

    /// <summary>Validates, stores the binary on disk, and records metadata + history.</summary>
    Task<Result<int>> AddAsync(int itemId, Stream content, string originalFileName, string contentType,
        AttachmentType type, string? description, CancellationToken ct = default);

    /// <summary>Opens an attachment's binary for streaming, or null if missing.</summary>
    Task<AttachmentContent?> OpenAsync(int attachmentId, CancellationToken ct = default);

    Task<Result> DeleteAsync(int attachmentId, CancellationToken ct = default);
}
