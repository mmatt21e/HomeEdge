namespace HomeStock.Web.Infrastructure;

/// <summary>SMTP settings (bound from the "Email" section). When Host and From are set, real
/// email is sent; otherwise HomeStock falls back to a no-op sender that only logs.</summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; } = true;

    /// <summary>From address, e.g. "homestock@example.com".</summary>
    public string? From { get; set; }
    public string? FromName { get; set; } = "HomeStock";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}
