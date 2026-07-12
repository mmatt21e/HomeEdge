using System.Security.Cryptography;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeStock.Infrastructure.Data;

/// <summary>Applies migrations and seeds roles, the initial admin, categories, settings, and
/// optional sample data. Idempotent: safe to run on every startup.</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}

public class DatabaseInitializer(
    ApplicationDbContext db,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<SeedOptions> seedOptions,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    private readonly SeedOptions _seed = seedOptions.Value;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(ct);

        await SeedRolesAsync();
        await SeedAdminAsync();
        await SeedCategoriesAsync(ct);
        await SeedSettingsAsync(ct);
        if (_seed.SeedSampleData) await SeedSampleDataAsync(ct);

        logger.LogInformation("Database initialization complete.");
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role {Role}", role);
            }
        }
    }

    private async Task SeedAdminAsync()
    {
        if (await userManager.Users.AnyAsync()) return; // an account already exists

        var password = string.IsNullOrWhiteSpace(_seed.AdminPassword)
            ? GenerateStrongPassword()
            : _seed.AdminPassword!;

        var admin = new ApplicationUser
        {
            UserName = _seed.AdminEmail,
            Email = _seed.AdminEmail,
            EmailConfirmed = true,
            DisplayName = "Administrator",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to create initial admin: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.Administrator);

        if (string.IsNullOrWhiteSpace(_seed.AdminPassword))
        {
            logger.LogWarning(
                "==================================================================\n" +
                " INITIAL ADMIN ACCOUNT CREATED\n" +
                "   Email:    {Email}\n" +
                "   Password: {Password}\n" +
                " Change this password after first login. Set Seed__AdminPassword\n" +
                " to control it explicitly.\n" +
                "==================================================================",
                _seed.AdminEmail, password);
        }
        else
        {
            logger.LogInformation("Initial admin account created for {Email}", _seed.AdminEmail);
        }
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;

        var defaults = new (string Name, string Icon, string Color)[]
        {
            ("Tools", "tools", "#f59e0b"),
            ("Electronics", "cpu", "#3b82f6"),
            ("Appliances", "plug", "#10b981"),
            ("Furniture", "house-door", "#8b5cf6"),
            ("Automotive", "car-front", "#ef4444"),
            ("Outdoor Equipment", "tree", "#22c55e"),
            ("Building Materials", "bricks", "#a16207"),
            ("Cleaning Supplies", "droplet", "#06b6d4"),
            ("Food Storage", "box-seam", "#f97316"),
            ("Documents", "file-earmark-text", "#64748b"),
            ("Replacement Parts", "gear", "#6366f1"),
            ("Seasonal Items", "snow", "#0ea5e9"),
            ("Safety Equipment", "shield-check", "#dc2626"),
            ("Personal Property", "person", "#ec4899"),
        };

        foreach (var (name, icon, color) in defaults)
            db.Categories.Add(new Category { Name = name, Icon = icon, Color = color, IsSystem = true });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} default categories", defaults.Length);
    }

    private async Task SeedSettingsAsync(CancellationToken ct)
    {
        async Task Ensure(string key, string value, string description)
        {
            if (!await db.Settings.AnyAsync(s => s.Key == key, ct))
                db.Settings.Add(new ApplicationSetting { Key = key, Value = value, Description = description });
        }

        await Ensure(SettingKeys.Currency, "USD", "ISO currency code used for value display.");
        await Ensure(SettingKeys.WarrantyWarningDays, "30", "Days before expiry to flag warranties.");
        await Ensure(SettingKeys.InstanceName, "HomeStock", "Display name for this installation.");
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSampleDataAsync(CancellationToken ct)
    {
        if (await db.Locations.AnyAsync(ct) || await db.Items.AnyAsync(ct)) return;

        var home = new Location { Name = "Home", Code = "LOC-HOME01" };
        var garage = new Location { Name = "Garage", Code = "LOC-GARAG1", Parent = home };
        var cabinet = new Location { Name = "Tool Cabinet", Code = "LOC-CABIN1", Parent = garage };
        var basement = new Location { Name = "Basement", Code = "LOC-BASEM1", Parent = home };
        db.Locations.AddRange(home, garage, cabinet, basement);
        await db.SaveChangesAsync(ct);

        var tools = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Tools", ct);
        var electronics = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Electronics", ct);

        db.Items.AddRange(
            new InventoryItem
            {
                Name = "Cordless Drill", CategoryId = tools?.Id, LocationId = cabinet.Id,
                Manufacturer = "DeWalt", ModelNumber = "DCD777", SerialNumber = "DW-00123",
                Barcode = "0885911475761", Quantity = 1, Condition = ItemCondition.Good,
                PurchasePrice = 99.00m, EstimatedValue = 70.00m,
                PurchaseDate = new DateOnly(2023, 5, 12), Status = ItemStatus.Available
            },
            new InventoryItem
            {
                Name = "Wi-Fi Router", CategoryId = electronics?.Id, LocationId = basement.Id,
                Manufacturer = "TP-Link", ModelNumber = "Archer AX55", Quantity = 1,
                Condition = ItemCondition.LikeNew, EstimatedValue = 90.00m,
                WarrantyExpiration = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
                Status = ItemStatus.InUse
            });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded sample locations and items");
    }

    private static string GenerateStrongPassword()
    {
        // 18 chars mixing classes to satisfy Identity's default policy.
        const string upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%*?";
        const string all = upper + lower + digits + special;
        Span<char> pw = stackalloc char[18];
        pw[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        pw[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        pw[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        pw[3] = special[RandomNumberGenerator.GetInt32(special.Length)];
        for (var i = 4; i < pw.Length; i++) pw[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        return new string(pw);
    }
}
