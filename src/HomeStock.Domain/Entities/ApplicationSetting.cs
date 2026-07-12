using HomeStock.Domain.Common;

namespace HomeStock.Domain.Entities;

/// <summary>
/// A single key/value application setting persisted in the database (e.g. default currency,
/// warranty-expiry warning window). Kept generic so new settings need no schema change.
/// </summary>
public class ApplicationSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? Description { get; set; }
}

/// <summary>Well-known setting keys.</summary>
public static class SettingKeys
{
    public const string Currency = "Currency";
    public const string WarrantyWarningDays = "WarrantyWarningDays";
    public const string InstanceName = "InstanceName";
}
