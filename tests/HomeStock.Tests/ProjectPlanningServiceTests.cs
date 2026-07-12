using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Xunit;

namespace HomeStock.Tests;

public class ProjectPlanningServiceTests
{
    /// <summary>Fake planner returning a fixed bill of materials (no network).</summary>
    private sealed class FakePlanner(IReadOnlyList<NeededItem> items, bool configured = true) : IProjectPlanner
    {
        public bool IsConfigured => configured;
        public string ProviderLabel => "fake";
        public Task<Result<IReadOnlyList<NeededItem>>> ProposeItemsAsync(string description, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<NeededItem>>.Success(items));
    }

    private static ProjectPlanningService Build(TestHarness h, IReadOnlyList<NeededItem> needed)
    {
        var ledger = new InventoryLedgerService(h.Db, new FakeCurrentUser(), h.Logger<InventoryLedgerService>());
        var projects = new ProjectService(h.Db, ledger, new FakeCurrentUser(), h.Logger<ProjectService>());
        return new ProjectPlanningService(new FakePlanner(needed), h.Db, ledger, projects);
    }

    private static void SeedInventory(TestHarness h)
    {
        var garage = new Location { Name = "Garage", Code = "LOC-PG1" };
        h.Db.Locations.Add(garage);
        h.Db.Items.AddRange(
            new InventoryItem { Name = "14/2 Romex Wire", Quantity = 100, Unit = "ft", Location = garage },
            new InventoryItem { Name = "Cordless Drill", Quantity = 1, Location = garage });
        h.Db.SaveChanges();
    }

    [Fact]
    public async Task Plan_Matches_Needed_Items_To_Inventory_And_Lists_Missing()
    {
        using var h = new TestHarness();
        SeedInventory(h);
        var needed = new List<NeededItem>
        {
            new() { Name = "electrical wire", Quantity = 25, Unit = "ft", Kind = ItemKind.Material, Keywords = new() { "romex" } },
            new() { Name = "drill", Kind = ItemKind.Tool },
            new() { Name = "junction box", Kind = ItemKind.Material }
        };
        var svc = Build(h, needed);

        var result = await svc.PlanAsync("wire up a new outlet");

        Assert.True(result.Succeeded);
        var plan = result.Value!;
        Assert.Equal(2, plan.InStock.Count());
        Assert.Single(plan.Missing);
        Assert.Contains(plan.Missing, m => m.Needed.Name == "junction box");

        var wire = plan.InStock.Single(m => m.MatchedItemName!.Contains("Romex"));
        Assert.Equal(100, wire.AvailableQuantity);   // nothing reserved yet
        Assert.Equal("Garage", wire.LocationName);
    }

    [Fact]
    public async Task CreateProjectFromPlan_Reserves_Selected_Items()
    {
        using var h = new TestHarness();
        SeedInventory(h);
        var svc = Build(h, new List<NeededItem>());
        var wireId = h.Db.Items.Single(i => i.Name.Contains("Romex")).Id;

        var result = await svc.CreateProjectFromPlanAsync("Outlet", "desc",
            new[] { new PlanSelection(wireId, 25, Consumable: true) });

        Assert.True(result.Succeeded);
        var ledger = new InventoryLedgerService(h.Db, new FakeCurrentUser(), h.Logger<InventoryLedgerService>());
        var status = await ledger.GetStatusAsync(wireId);
        Assert.Equal(25, status!.CheckedOut);
        Assert.Equal(75, status.Available);
        Assert.Single(h.Db.ProjectAllocations);
    }

    [Fact]
    public async Task Available_Reflects_Existing_Reservations()
    {
        using var h = new TestHarness();
        SeedInventory(h);
        var wireId = h.Db.Items.Single(i => i.Name.Contains("Romex")).Id;
        var ledger = new InventoryLedgerService(h.Db, new FakeCurrentUser(), h.Logger<InventoryLedgerService>());
        await ledger.CheckOutAsync(wireId, 40, "other project");

        var svc = Build(h, new List<NeededItem>
        {
            new() { Name = "romex wire", Kind = ItemKind.Material }
        });
        var plan = (await svc.PlanAsync("more wiring")).Value!;

        Assert.Equal(60, plan.InStock.Single().AvailableQuantity); // 100 - 40 reserved
    }
}
