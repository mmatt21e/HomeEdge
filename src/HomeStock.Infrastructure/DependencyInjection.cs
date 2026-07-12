using HomeStock.Application.Abstractions;
using HomeStock.Infrastructure.Data;
using HomeStock.Infrastructure.Services;
using HomeStock.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeStock.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the DbContext (provider selected by configuration), storage, seeding and
    /// application-layer service implementations. Keeping provider choice here means the switch
    /// from SQLite to PostgreSQL is a configuration change, not a code change.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Database:Provider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=data/homestock.db";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            switch (provider.Trim().ToLowerInvariant())
            {
                case "postgres":
                case "postgresql":
                case "npgsql":
                    options.UseNpgsql(connectionString, npg =>
                        npg.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                           .MigrationsHistoryTable("__EFMigrationsHistory"));
                    break;
                default:
                    EnsureSqliteDirectory(connectionString);
                    options.UseSqlite(connectionString, sqlite =>
                        sqlite.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
                    break;
            }
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<ICodeGenerator, CodeGenerator>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        // Photo -> inventory extraction (any OpenAI-compatible vision endpoint; disabled until configured).
        services.Configure<Vision.VisionOptions>(configuration.GetSection(Vision.VisionOptions.SectionName));
        services.AddHttpClient(Vision.OpenAiCompatibleVisionExtractor.HttpClientName);
        services.AddScoped<IVisionExtractor, Vision.OpenAiCompatibleVisionExtractor>();

        return services;
    }

    private static void EnsureSqliteDirectory(string connectionString)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource)) return;
        var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }
}
