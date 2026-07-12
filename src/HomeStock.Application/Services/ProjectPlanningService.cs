using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeStock.Application.Services;

/// <summary>
/// Orchestrates AI project planning: asks the planner what a described job needs, then matches
/// that against the real inventory locally (token overlap on names/keywords/category) so the
/// reported locations and stock are always grounded in actual items.
/// </summary>
public class ProjectPlanningService(
    IProjectPlanner planner,
    IApplicationDbContext db,
    IInventoryLedgerService ledger,
    IProjectService projects) : IProjectPlanningService
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "for", "and", "or", "with", "to", "in", "kit", "set", "new", "some"
    };

    public bool IsConfigured => planner.IsConfigured;
    public string ProviderLabel => planner.ProviderLabel;

    public async Task<Result<ProjectPlan>> PlanAsync(string description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description))
            return Result<ProjectPlan>.Failure("Describe the project first.");

        var proposed = await planner.ProposeItemsAsync(description.Trim(), ct);
        if (!proposed.Succeeded) return Result<ProjectPlan>.Failure(proposed.Errors);
        var needed = proposed.Value!;
        if (needed.Count == 0)
            return Result<ProjectPlan>.Failure("The planner didn't identify any items. Try describing the project in more detail.");

        // Load the active inventory once and match in memory.
        var inventory = await db.Items.AsNoTracking()
            .Where(i => !i.IsArchived)
            .Select(i => new InventoryRow(
                i.Id, i.Name, i.Category != null ? i.Category.Name : null,
                i.Quantity, i.Unit, i.Location != null ? i.Location.Name : null, i.Container))
            .ToListAsync(ct);

        var indexed = inventory.Select(r => (row: r, tokens: Tokenize($"{r.Name} {r.Category}"))).ToList();

        var matches = new List<PlanMatch>();
        foreach (var n in needed)
        {
            var wanted = Tokenize(string.Join(' ', new[] { n.Name, n.Category }.Concat(n.Keywords)));
            var best = indexed
                .Select(x => (x.row, score: Score(wanted, n.Name, x.tokens, x.row.Name)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .FirstOrDefault();

            if (best.row is null)
            {
                matches.Add(new PlanMatch(n, null, null, null, n.Unit, null, null));
                continue;
            }

            // Ground the available quantity in the ledger (respects existing reservations).
            var status = await ledger.GetStatusAsync(best.row.Id, ct);
            matches.Add(new PlanMatch(n, best.row.Id, best.row.Name,
                status?.Available ?? best.row.Quantity, best.row.Unit, best.row.LocationName, best.row.Container));
        }

        return Result<ProjectPlan>.Success(new ProjectPlan(description.Trim(), matches, planner.ProviderLabel));
    }

    public async Task<Result<int>> CreateProjectFromPlanAsync(string name, string? description, IReadOnlyList<PlanSelection> selections, CancellationToken ct = default)
    {
        var created = await projects.CreateAsync(new ProjectEditModel { Name = name, Description = description }, ct);
        if (!created.Succeeded) return Result<int>.Failure(created.Errors);
        var projectId = created.Value;

        var warnings = new List<string>();
        foreach (var s in selections.Where(s => s.Quantity > 0))
        {
            var r = await projects.AddAllocationAsync(projectId, s.ItemId, s.Quantity, s.Consumable, "from AI plan", ct);
            if (!r.Succeeded) warnings.AddRange(r.Errors);
        }
        return Result<int>.Success(projectId, warnings);
    }

    // --- matching helpers ---

    private static int Score(HashSet<string> wanted, string neededName, HashSet<string> itemTokens, string itemName)
    {
        var shared = wanted.Count(itemTokens.Contains);
        var score = shared * 2;
        // Bonus for substring containment either way (handles multi-word names).
        var a = neededName.Trim().ToLowerInvariant();
        var b = itemName.Trim().ToLowerInvariant();
        if (a.Length > 2 && (b.Contains(a) || a.Contains(b))) score += 3;
        return score;
    }

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split(new[] { ' ', ',', '/', '-', '(', ')', '.', ';', ':' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = new string(raw.Where(char.IsLetterOrDigit).ToArray());
            if (t.Length >= 3 && !Stopwords.Contains(t)) tokens.Add(t.ToLowerInvariant());
        }
        return tokens;
    }

    private record InventoryRow(int Id, string Name, string? Category, decimal Quantity, string? Unit, string? LocationName, string? Container);
}
