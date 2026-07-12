using System.ComponentModel.DataAnnotations;

namespace HomeStock.Application.Models;

public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    bool IsSystem,
    bool IsArchived,
    int ItemCount);

/// <summary>Input model for creating/editing a category. Annotated for centralized validation.</summary>
public class CategoryEditModel
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [RegularExpression("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Color must be a hex value like #4f46e5.")]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }
}
