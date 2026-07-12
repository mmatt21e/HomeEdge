using System.ComponentModel.DataAnnotations;

namespace HomeStock.Application.Models;

public record LoanDto(
    int Id,
    int ItemId,
    string? ItemName,
    string BorrowerName,
    string? BorrowerContact,
    DateOnly LoanDate,
    DateOnly? ExpectedReturnDate,
    DateOnly? ActualReturnDate,
    string? Notes)
{
    public bool IsOpen => ActualReturnDate is null;

    /// <summary>An open loan is overdue when its expected return date has passed.</summary>
    public bool IsOverdue => IsOpen && ExpectedReturnDate is { } due && due < DateOnly.FromDateTime(DateTime.UtcNow);
}

/// <summary>Input to loan an item out.</summary>
public class LoanEditModel
{
    [Required, StringLength(160, MinimumLength = 1)]
    public string BorrowerName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? BorrowerContact { get; set; }

    public DateOnly LoanDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? ExpectedReturnDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
