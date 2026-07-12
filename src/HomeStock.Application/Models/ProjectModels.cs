using System.ComponentModel.DataAnnotations;
using HomeStock.Domain.Enums;

namespace HomeStock.Application.Models;

public record ProjectListDto(
    int Id,
    string Name,
    ProjectStatus Status,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record ProjectAllocationDto(
    int Id,
    int ItemId,
    string ItemName,
    string? Unit,
    decimal QuantityAllocated,
    decimal QuantityConsumed,
    bool Consumable,
    string? LocationName,
    string? Container)
{
    public string LocationDisplay =>
        string.IsNullOrEmpty(LocationName)
            ? "No location"
            : string.IsNullOrEmpty(Container) ? LocationName : $"{LocationName} / {Container}";
}

public record ProjectDto(
    int Id,
    string Name,
    string? Description,
    ProjectStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ProjectAllocationDto> Allocations);

public class ProjectEditModel
{
    [Required, StringLength(160, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
}

/// <summary>Per-allocation input at project close-out: how much of the reserved amount was used.</summary>
public record CloseoutLine(int AllocationId, decimal UsedAmount);
