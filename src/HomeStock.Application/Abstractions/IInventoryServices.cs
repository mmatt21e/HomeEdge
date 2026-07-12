using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<CategoryDto?> GetAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(CategoryEditModel model, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, CategoryEditModel model, CancellationToken ct = default);
    Task<Result> ArchiveAsync(int id, CancellationToken ct = default);
    Task<Result> RestoreAsync(int id, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface ILocationService
{
    Task<IReadOnlyList<LocationDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<IReadOnlyList<LocationTreeNode>> GetTreeAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<LocationDto?> GetAsync(int id, CancellationToken ct = default);
    Task<LocationDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    /// <summary>Ids of a location and all of its descendants (for sublocation-inclusive queries).</summary>
    Task<IReadOnlyList<int>> GetSelfAndDescendantIdsAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(LocationEditModel model, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, LocationEditModel model, CancellationToken ct = default);
    Task<Result> MoveAsync(int id, int? newParentId, CancellationToken ct = default);
    Task<Result> ArchiveAsync(int id, CancellationToken ct = default);
    Task<Result> RestoreAsync(int id, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IItemService
{
    Task<PagedResult<ItemListDto>> SearchAsync(ItemQuery query, CancellationToken ct = default);
    Task<ItemDto?> GetAsync(int id, CancellationToken ct = default);
    Task<ItemDto?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(ItemEditModel model, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, ItemEditModel model, CancellationToken ct = default);
    Task<Result> ArchiveAsync(int id, CancellationToken ct = default);
    Task<Result> RestoreAsync(int id, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Warnings (not errors) if serial/barcode already exist on another item.</summary>
    Task<IReadOnlyList<string>> CheckDuplicatesAsync(string? serial, string? barcode, int? excludeItemId, CancellationToken ct = default);

    /// <summary>Assigns a scanned barcode to an existing item and records history.</summary>
    Task<Result> AssignBarcodeAsync(int itemId, string barcode, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

public interface IItemHistoryService
{
    Task<IReadOnlyList<Models.ItemHistoryDto>> GetForItemAsync(int itemId, CancellationToken ct = default);

    /// <summary>Most recent change-log entries across all items (for the admin activity feed).</summary>
    Task<IReadOnlyList<Models.RecentActivityDto>> GetRecentAsync(int limit = 25, CancellationToken ct = default);
}
