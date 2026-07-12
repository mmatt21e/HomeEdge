using HomeStock.Domain.Enums;

namespace HomeStock.Application.Models;

public record ItemHistoryDto(
    int Id,
    int ItemId,
    HistoryAction Action,
    string? UserName,
    string? Summary,
    string? ChangesJson,
    DateTime Timestamp);

/// <summary>A change-log entry with the item name, for the cross-item activity feed.</summary>
public record RecentActivityDto(
    int Id,
    int ItemId,
    string? ItemName,
    HistoryAction Action,
    string? UserName,
    string? Summary,
    DateTime Timestamp);
