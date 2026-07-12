namespace HomeStock.Application.Abstractions;

/// <summary>Identifies the acting user for history/audit stamping. Implemented in the Web layer.</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}

/// <summary>
/// Stores and retrieves attachment binaries outside the database. Implemented in
/// Infrastructure over the local filesystem; the interface allows swapping to object storage.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Persists a stream and returns a safe generated relative path. The caller supplies the
    /// original name only to derive a safe extension; the stored name is never caller-controlled.
    /// </summary>
    Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default);

    Task<Stream?> OpenReadAsync(string storedPath, CancellationToken ct = default);

    Task DeleteAsync(string storedPath, CancellationToken ct = default);

    /// <summary>Absolute path on disk for a stored relative path (used by health checks / serving).</summary>
    string ResolvePhysicalPath(string storedPath);

    /// <summary>True when the storage root exists and is writable (used by health checks).</summary>
    bool IsHealthy(out string? detail);
}

/// <summary>Generates stable, collision-resistant codes for location QR labels and item labels.</summary>
public interface ICodeGenerator
{
    string NewLocationCode();
    string NewItemLabelCode();
}
