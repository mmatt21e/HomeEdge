namespace HomeStock.Application.Models;

public record CountByName(string Name, int Count);

/// <summary>Aggregate figures and lists shown on the dashboard.</summary>
public record DashboardDto(
    int TotalItemRecords,
    decimal TotalQuantity,
    decimal EstimatedTotalValue,
    int LoanedOutCount,
    int MissingPhotoCount,
    int WithoutLocationCount,
    int WarrantiesExpiringSoonCount,
    IReadOnlyList<ItemListDto> RecentlyAdded,
    IReadOnlyList<ItemListDto> RecentlyUpdated,
    IReadOnlyList<ItemListDto> WarrantiesExpiringSoon,
    IReadOnlyList<ItemListDto> LoanedItems,
    IReadOnlyList<CountByName> CountByCategory,
    IReadOnlyList<CountByName> CountByLocation);
