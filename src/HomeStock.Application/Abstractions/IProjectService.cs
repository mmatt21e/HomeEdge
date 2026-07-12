using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectListDto>> GetAllAsync(bool includeClosed = true, CancellationToken ct = default);
    Task<ProjectDto?> GetAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(ProjectEditModel model, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, ProjectEditModel model, CancellationToken ct = default);

    /// <summary>Reserves an item for the project (checks the quantity out of inventory).</summary>
    Task<Result> AddAllocationAsync(int projectId, int itemId, decimal quantity, bool consumable, string? note, CancellationToken ct = default);

    /// <summary>Changes a reservation, checking out or returning the difference.</summary>
    Task<Result> SetAllocationQuantityAsync(int allocationId, decimal newQuantity, CancellationToken ct = default);

    /// <summary>Removes a reservation, returning the whole amount to inventory.</summary>
    Task<Result> RemoveAllocationAsync(int allocationId, CancellationToken ct = default);

    /// <summary>Completes the project: consumes the "used" amounts and returns the rest.</summary>
    Task<Result> CompleteAsync(int projectId, IReadOnlyList<CloseoutLine> lines, CancellationToken ct = default);

    /// <summary>Cancels the project: returns every reserved amount, nothing consumed.</summary>
    Task<Result> CancelAsync(int projectId, CancellationToken ct = default);

    Task<Result> DeleteAsync(int projectId, CancellationToken ct = default);
}
