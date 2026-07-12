using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HomeStock.Tests.Integration;

/// <summary>
/// Boots the real application in-process (its own temp SQLite DB and attachment dir) so tests
/// exercise the actual middleware pipeline: auth, headers, health checks, routing.
/// </summary>
public class WebFactory : WebApplicationFactory<Program>
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "hs-it-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        Directory.CreateDirectory(_dir);
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(_dir, "it.db")};Cache=Shared");
        builder.UseSetting("Storage:AttachmentsPath", Path.Combine(_dir, "attachments"));
        builder.UseSetting("Seed:AdminEmail", "admin@homestock.local");
        builder.UseSetting("Seed:AdminPassword", "Admin123!Pass");
        builder.UseSetting("Seed:SeedSampleData", "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { /* best effort */ }
    }
}
