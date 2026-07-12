using HomeStock.Domain.Enums;

namespace HomeStock.Application.Models;

/// <summary>Current stock position for an item, derived from the ledger.</summary>
public record StockStatus(decimal OnHand, decimal CheckedOut, string? Unit)
{
    /// <summary>What can still be taken: on-hand minus what's currently checked out.</summary>
    public decimal Available => Math.Max(0, OnHand - CheckedOut);

    public string OnHandDisplay => CommonUnits.Format(OnHand, Unit);
    public string AvailableDisplay => CommonUnits.Format(Available, Unit);
    public string CheckedOutDisplay => CommonUnits.Format(CheckedOut, Unit);
}

public record TransactionDto(
    int Id,
    int ItemId,
    TransactionType Type,
    decimal Quantity,
    decimal BalanceAfter,
    string? Unit,
    string? Note,
    int? ProjectId,
    string? UserName,
    DateTime Timestamp);
