using System.ComponentModel.DataAnnotations;

namespace HomeStock.Application.Models;

public record LocationDto(
    int Id,
    string Name,
    string? Description,
    int? ParentId,
    string Code,
    string? PhotoPath,
    bool IsArchived,
    int DirectItemCount);

/// <summary>A location plus its depth in the tree, for rendering an indented hierarchy.</summary>
public record LocationTreeNode(LocationDto Location, int Depth)
{
    /// <summary>Full path from the root, e.g. "Home / Garage / Tool Cabinet".</summary>
    public string Path { get; init; } = string.Empty;
}

public class LocationEditModel
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public int? ParentId { get; set; }
}
