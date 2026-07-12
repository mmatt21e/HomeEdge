namespace HomeStock.Application.Models;

/// <summary>Validation summary for an uploaded JSON backup, shown before a restore is applied.</summary>
public record BackupSummary(
    int SchemaVersion,
    DateTime? ExportedAtUtc,
    int CategoryCount,
    int LocationCount,
    int ItemCount,
    int TagCount,
    bool CurrentDatabaseHasItems,
    IReadOnlyList<string> Warnings);

/// <summary>Outcome of an additive (merge) restore.</summary>
public record RestoreResult(
    int CategoriesAdded,
    int LocationsAdded,
    int ItemsAdded,
    int ItemsSkipped);
