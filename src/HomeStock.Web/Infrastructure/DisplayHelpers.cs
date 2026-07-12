using HomeStock.Application.Models;
using HomeStock.Domain.Enums;

namespace HomeStock.Web.Infrastructure;

/// <summary>UI formatting helpers for enum labels, colours and derived warranty state.</summary>
public static class DisplayHelpers
{
    public static string Label(ItemStatus s) => s switch
    {
        ItemStatus.InUse => "In use",
        _ => SplitPascal(s.ToString())
    };

    public static string Label(ItemCondition c) => c switch
    {
        ItemCondition.LikeNew => "Like new",
        ItemCondition.ForPartsOrRepair => "For parts / repair",
        _ => SplitPascal(c.ToString())
    };

    /// <summary>Bootstrap contextual class suffix for a status badge.</summary>
    public static string StatusClass(ItemStatus s) => s switch
    {
        ItemStatus.Available => "success",
        ItemStatus.InUse => "primary",
        ItemStatus.Loaned => "info",
        ItemStatus.Missing => "warning",
        ItemStatus.Damaged => "danger",
        ItemStatus.Disposed or ItemStatus.Sold or ItemStatus.Donated => "secondary",
        ItemStatus.Archived => "dark",
        _ => "secondary"
    };

    public static string ConditionClass(ItemCondition c) => c switch
    {
        ItemCondition.New or ItemCondition.LikeNew => "success",
        ItemCondition.Good => "primary",
        ItemCondition.Fair => "warning",
        ItemCondition.Poor or ItemCondition.ForPartsOrRepair => "danger",
        _ => "secondary"
    };

    public static WarrantyState WarrantyStateOf(DateOnly? expiry, int warnDays = 30)
    {
        if (expiry is null) return WarrantyState.None;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (expiry < today) return WarrantyState.Expired;
        if (expiry <= today.AddDays(warnDays)) return WarrantyState.ExpiringSoon;
        return WarrantyState.Active;
    }

    public static (string Text, string Class) WarrantyBadge(DateOnly? expiry, int warnDays = 30)
        => WarrantyStateOf(expiry, warnDays) switch
        {
            WarrantyState.Expired => ("Expired", "danger"),
            WarrantyState.ExpiringSoon => ("Expiring soon", "warning"),
            WarrantyState.Active => ("Under warranty", "success"),
            _ => ("", "")
        };

    private static string SplitPascal(string value)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i])) sb.Append(' ');
            sb.Append(value[i]);
        }
        return sb.ToString();
    }
}
