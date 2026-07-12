namespace HomeStock.Web.Infrastructure;

public record NavItem(string Label, string Href, string Icon, bool InBottomNav, bool Match = false);

/// <summary>Single source of truth for the primary navigation (sidebar + mobile bottom bar).</summary>
public static class NavItems
{
    public static readonly IReadOnlyList<NavItem> All = new List<NavItem>
    {
        new("Dashboard", "",           "bi-speedometer2",      InBottomNav: true,  Match: true),
        new("Items",     "items",      "bi-box-seam",          InBottomNav: true),
        new("Scan",      "scan",       "bi-upc-scan",          InBottomNav: true),
        new("Locations", "locations",  "bi-geo-alt",           InBottomNav: true),
        new("Categories","categories", "bi-tags",              InBottomNav: false),
        new("Audits",    "audits",     "bi-clipboard-check",   InBottomNav: false),
        new("Reports",   "reports",    "bi-file-earmark-bar-graph", InBottomNav: false),
        new("Settings",  "settings",   "bi-gear",              InBottomNav: false),
    };

    public static IEnumerable<NavItem> BottomNav => All.Where(i => i.InBottomNav).Take(4)
        .Append(new NavItem("More", "menu", "bi-list", true));
}
