namespace HomeStock.Infrastructure.Storage;

/// <summary>Configuration for on-disk attachment storage (bound from the "Storage" section).</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Absolute or relative root directory where attachment binaries are written.</summary>
    public string AttachmentsPath { get; set; } = "data/attachments";

    /// <summary>Maximum allowed upload size in bytes (default 20 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Permitted file extensions (lower-case, incl. dot). Empty = allow the built-in safe set.</summary>
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
}
