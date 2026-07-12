using HomeStock.Application.Abstractions;
using HomeStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeStock.Application.Services;

public interface ISettingsService
{
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);
}

public class SettingsService(IApplicationDbContext db) : ISettingsService
{
    public async Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default) =>
        await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        (await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct))?.Value;

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
            db.Settings.Add(new ApplicationSetting { Key = key, Value = value });
        else
            setting.Value = value;
        await db.SaveChangesAsync(ct);
    }
}
