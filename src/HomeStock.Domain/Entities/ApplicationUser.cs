using Microsoft.AspNetCore.Identity;

namespace HomeStock.Domain.Entities;

/// <summary>
/// Application user. Extends the ASP.NET Core Identity user with a display name and
/// activation flag. Roles (Administrator / StandardUser / ReadOnly) are managed via
/// Identity role assignments rather than a column here.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
