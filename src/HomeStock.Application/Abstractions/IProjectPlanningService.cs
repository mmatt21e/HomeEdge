using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

public interface IProjectPlanningService
{
    bool IsConfigured { get; }
    string ProviderLabel { get; }

    /// <summary>Proposes a bill of materials for a described project and matches it to inventory.</summary>
    Task<Result<ProjectPlan>> PlanAsync(string description, CancellationToken ct = default);

    /// <summary>Creates a project and reserves the selected matched items.</summary>
    Task<Result<int>> CreateProjectFromPlanAsync(string name, string? description, IReadOnlyList<PlanSelection> selections, CancellationToken ct = default);
}
