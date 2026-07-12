using HomeStock.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeStock.Infrastructure.Storage;

/// <summary>
/// Stores attachment binaries on the local filesystem under a configured root. Files are saved
/// with server-generated names (never the client-supplied filename) sharded into subdirectories
/// to avoid huge flat folders. The original name is preserved only as metadata by the caller.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> DefaultAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".bmp",
        ".pdf", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".csv"
    };

    private readonly StorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _root;

    public LocalFileStorageService(IOptions<StorageOptions> options, ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _root = Path.GetFullPath(_options.AttachmentsPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var allowed = _options.AllowedExtensions.Length > 0
            ? new HashSet<string>(_options.AllowedExtensions, StringComparer.OrdinalIgnoreCase)
            : DefaultAllowed;
        if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
            throw new InvalidOperationException($"File type '{ext}' is not allowed.");

        // Server-generated name; two-level shard from the GUID keeps directories small.
        var id = Guid.NewGuid().ToString("N");
        var relative = Path.Combine(id[..2], id[2..4], id + ext);
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using (var fs = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs, ct);
            if (fs.Length > _options.MaxFileSizeBytes)
            {
                fs.Close();
                File.Delete(full);
                throw new InvalidOperationException("File exceeds the maximum allowed size.");
            }
        }

        _logger.LogInformation("Stored attachment {Relative} ({ContentType})", relative, contentType);
        // Normalise to forward slashes for portable relative paths.
        return relative.Replace('\\', '/');
    }

    public Task<Stream?> OpenReadAsync(string storedPath, CancellationToken ct = default)
    {
        var full = ResolvePhysicalPath(storedPath);
        if (!File.Exists(full)) return Task.FromResult<Stream?>(null);
        Stream stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storedPath, CancellationToken ct = default)
    {
        var full = ResolvePhysicalPath(storedPath);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    public string ResolvePhysicalPath(string storedPath)
    {
        // Prevent path traversal: the resolved path must stay under the storage root.
        var full = Path.GetFullPath(Path.Combine(_root, storedPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage path.");
        return full;
    }

    public bool IsHealthy(out string? detail)
    {
        try
        {
            Directory.CreateDirectory(_root);
            var probe = Path.Combine(_root, ".healthcheck");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            detail = _root;
            return true;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }
}
