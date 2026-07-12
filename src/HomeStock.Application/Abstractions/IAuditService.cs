using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Enums;

namespace HomeStock.Application.Abstractions;

public interface IAuditService
{
    Task<IReadOnlyList<AuditListDto>> GetHistoryAsync(CancellationToken ct = default);

    Task<AuditDto?> GetAsync(int auditId, CancellationToken ct = default);

    /// <summary>Starts an audit for a location, snapshotting the items expected to be there.</summary>
    Task<Result<int>> StartAsync(int locationId, bool includeSublocations, CancellationToken ct = default);

    /// <summary>Records the outcome for one expected item during the audit.</summary>
    Task<Result> RecordResultAsync(int auditItemId, AuditItemResult result, int? movedToLocationId, string? notes, CancellationToken ct = default);

    /// <summary>Marks an expected item confirmed by scanning its barcode; returns the row updated.</summary>
    Task<Result<int>> ConfirmByScanAsync(int auditId, string code, CancellationToken ct = default);

    /// <summary>Completes the audit, applying moved/missing/damaged outcomes to the items.</summary>
    Task<Result> CompleteAsync(int auditId, string? notes, CancellationToken ct = default);

    Task<Result> CancelAsync(int auditId, CancellationToken ct = default);
}
