using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

public interface IBackupRestoreService
{
    /// <summary>Parses and validates a JSON backup without applying it.</summary>
    Task<Result<BackupSummary>> ValidateAsync(Stream json, CancellationToken ct = default);

    /// <summary>
    /// Restores a JSON backup additively (merge): missing categories, locations, tags and items
    /// are created; items that already exist (same name + serial + barcode) are skipped. This is
    /// non-destructive — it never deletes existing data.
    /// </summary>
    Task<Result<RestoreResult>> RestoreAsync(Stream json, CancellationToken ct = default);
}
