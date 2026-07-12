using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomeStock.Infrastructure.Data;

/// <summary>
/// Enables `dotnet ef` to construct the context at design time without the Web host.
/// Migrations are always generated against SQLite so a single migration set works for the
/// default self-hosted install; PostgreSQL users generate their own set with the pg provider.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("HOMESTOCK_MIGRATIONS_PROVIDER") ?? "Sqlite";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql("Host=localhost;Database=homestock;Username=postgres;Password=postgres",
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        }
        else
        {
            options.UseSqlite("Data Source=homestock-design.db",
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        }

        return new ApplicationDbContext(options.Options);
    }
}
