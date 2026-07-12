namespace HomeStock.Application.Models;

/// <summary>Whether a needed thing is a consumable material or a reusable tool.</summary>
public enum ItemKind { Unknown, Material, Tool }

/// <summary>An item the planner believes a described project requires.</summary>
public class NeededItem
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public ItemKind Kind { get; set; } = ItemKind.Unknown;
    /// <summary>Extra search terms/synonyms the planner suggests for matching (e.g. "romex").</summary>
    public List<string> Keywords { get; set; } = new();
}

/// <summary>A needed item paired with the best inventory match found locally (if any).</summary>
public record PlanMatch(
    NeededItem Needed,
    int? MatchedItemId,
    string? MatchedItemName,
    decimal? AvailableQuantity,
    string? Unit,
    string? LocationName,
    string? Container)
{
    public bool InStock => MatchedItemId is not null;

    public string LocationDisplay =>
        string.IsNullOrEmpty(LocationName) ? "No location"
            : string.IsNullOrEmpty(Container) ? LocationName : $"{LocationName} / {Container}";
}

/// <summary>The full result of planning a described project against inventory.</summary>
public record ProjectPlan(
    string Description,
    IReadOnlyList<PlanMatch> Matches,
    string ProviderLabel)
{
    public IEnumerable<PlanMatch> InStock => Matches.Where(m => m.InStock);
    public IEnumerable<PlanMatch> Missing => Matches.Where(m => !m.InStock);
}

/// <summary>A user's choice of what to reserve when turning a plan into a project.</summary>
public record PlanSelection(int ItemId, decimal Quantity, bool Consumable);
