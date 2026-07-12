using HomeStock.Application.Abstractions;
using HomeStock.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeStock.Tests;

/// <summary>
/// Spins up a real (in-memory SQLite) ApplicationDbContext with the production schema so tests
/// exercise the actual EF configuration and query translation, not a mock.
/// </summary>
public sealed class TestHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    public ApplicationDbContext Db { get; }

    public TestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        Db = new ApplicationDbContext(options);
        Db.Database.EnsureCreated();
    }

    public NullLogger<T> Logger<T>() => NullLogger<T>.Instance;

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

/// <summary>Deterministic code generator for tests.</summary>
public class FakeCodeGenerator : ICodeGenerator
{
    private int _n;
    public string NewLocationCode() => $"LOC-T{_n++:D4}";
    public string NewItemLabelCode() => $"ITM-T{_n++:D4}";
}

public class FakeCurrentUser(string? id = "u1", string? name = "tester") : ICurrentUserService
{
    public string? UserId { get; } = id;
    public string? UserName { get; } = name;
    public bool IsAuthenticated => UserId is not null;
}

/// <summary>Creates a real <see cref="LocalFileStorageService"/> rooted in a throwaway temp dir.</summary>
public sealed class TempFileStorage : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "hs-tests-" + Guid.NewGuid().ToString("N"));
    public HomeStock.Infrastructure.Storage.LocalFileStorageService Service { get; }

    public TempFileStorage()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new HomeStock.Infrastructure.Storage.StorageOptions { AttachmentsPath = Root });
        Service = new HomeStock.Infrastructure.Storage.LocalFileStorageService(options, NullLogger<HomeStock.Infrastructure.Storage.LocalFileStorageService>.Instance);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
    }
}
