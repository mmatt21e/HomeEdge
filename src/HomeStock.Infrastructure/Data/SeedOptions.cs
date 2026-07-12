namespace HomeStock.Infrastructure.Data;

/// <summary>First-run seeding configuration (bound from the "Seed" section / environment).</summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Email/username for the initial administrator account.</summary>
    public string AdminEmail { get; set; } = "admin@homestock.local";

    /// <summary>
    /// Password for the initial administrator. If left blank, a strong random password is
    /// generated on first run and written to the application log for the operator to retrieve.
    /// Never hardcode a real password here.
    /// </summary>
    public string? AdminPassword { get; set; }

    /// <summary>When true, inserts sample items/locations for development/demo purposes.</summary>
    public bool SeedSampleData { get; set; }
}
