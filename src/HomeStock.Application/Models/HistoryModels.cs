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
