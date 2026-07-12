using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

public interface IImportExportService
{
    /// <summary>Exports items matching the query as a CSV file.</summary>
    Task<ExportFile> ExportItemsCsvAsync(ItemQuery query, CancellationToken ct = default);

    /// <summary>Exports the full inventory (items, categories, locations, tags) as a JSON backup.</summary>
    Task<ExportFile> ExportJsonBackupAsync(CancellationToken ct = default);

    /// <summary>Reads a CSV and returns headers, sample rows, and a suggested column mapping.</summary>
    Task<Result<ImportPreview>> PreviewCsvAsync(Stream csv, CancellationToken ct = default);

    /// <summary>Validates every row against the mapping: errors, warnings, and duplicate detection.</summary>
    Task<Result<ImportValidation>> ValidateCsvAsync(Stream csv, IReadOnlyDictionary<string, string> mapping, CancellationToken ct = default);

    /// <summary>Imports the valid, non-duplicate rows. Duplicates and invalid rows are skipped.</summary>
    Task<Result<ImportCommitResult>> CommitCsvAsync(Stream csv, IReadOnlyDictionary<string, string> mapping, bool skipDuplicates, CancellationToken ct = default);
}
