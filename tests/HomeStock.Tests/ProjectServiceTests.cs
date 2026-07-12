using HomeStock.Application.Models;
using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Xunit;

namespace HomeStock.Tests;

public class ProjectServiceTests
{
    private static (ProjectService projects, InventoryLedgerService ledger) Build(TestHarness h)
    {
        var ledger = new InventoryLedgerService(h.Db, new FakeCurrentUser(), h.Logger<InventoryLedgerService>());
        var projects = new ProjectService(h.Db, ledger, new FakeCurrentUser(), h.Logger<ProjectService>());
        return (projects, ledger);
    }

    private static int NewItem(TestHarness h, string name, decimal qty, string? unit = null)
    {
        var item = new InventoryItem { Name = name, Quantity = qty, Unit = unit };
        h.Db.Items.Add(item);
        h.Db.SaveChanges();
        return item.Id;
    }

    [Fact]
    public async Task AddAllocation_Reserves_Stock_From_Inventory()
    {
        using var h = new TestHarness();
        var (projects, ledger) = Build(h);
        var wire = NewItem(h, "Wire", 100, "ft");
        var pid = (await projects.CreateAsync(new ProjectEditModel { Name = "Circuit" })).Value;

        Assert.True((await projects.AddAllocationAsync(pid, wire, 25, consumable: true, note: null)).Succeeded);

        var status = await ledger.GetStatusAsync(wire);
        Assert.Equal(100, status!.OnHand);     // still owned
        Assert.Equal(25, status.CheckedOut);   // but reserved
        Assert.Equal(75, status.Available);
    }

    [Fact]
    public async Task Cannot_Reserve_More_Than_Available()
    {
        using var h = new TestHarness();
        var (projects, _) = Build(h);
        var item = NewItem(h, "Screws", 10);
        var pid = (await projects.CreateAsync(new ProjectEditModel { Name = "P" })).Value;

        var r = await projects.AddAllocationAsync(pid, item, 20, false, null);

        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Adding_Same_Item_Twice_Increases_The_Allocation()
    {
        using var h = new TestHarness();
        var (projects, ledger) = Build(h);
        var wire = NewItem(h, "Wire", 100, "ft");
        var pid = (await projects.CreateAsync(new ProjectEditModel { Name = "P" })).Value;

        await projects.AddAllocationAsync(pid, wire, 25, true, null);
        await projects.AddAllocationAsync(pid, wire, 10, true, null);

        var project = await projects.GetAsync(pid);
        Assert.Single(project!.Allocations);
        Assert.Equal(35, project.Allocations[0].QuantityAllocated);
        Assert.Equal(35, (await ledger.GetStatusAsync(wire))!.CheckedOut);
    }

    [Fact]
    public async Task Complete_Consumes_Materials_And_Returns_Tools()
    {
        using var h = new TestHarness();
        var (projects, ledger) = Build(h);
        var wire = NewItem(h, "Wire", 100, "ft");
        var drill = NewItem(h, "Drill", 1);
        var pid = (await projects.CreateAsync(new ProjectEditModel { Name = "Circuit" })).Value;
        await projects.AddAllocationAsync(pid, wire, 25, consumable: true, note: null);
        await projects.AddAllocationAsync(pid, drill, 1, consumable: false, note: null);

        var project = await projects.GetAsync(pid);
        var wireAlloc = project!.Allocations.Single(a => a.ItemId == wire);
        var drillAlloc = project.Allocations.Single(a => a.ItemId == drill);

        // Used all 25 ft of wire; the drill (0 used) comes back.
        var r = await projects.CompleteAsync(pid, new[]
        {
            new CloseoutLine(wireAlloc.Id, 25),
            new CloseoutLine(drillAlloc.Id, 0)
        });

        Assert.True(r.Succeeded);
        var wireStatus = await ledger.GetStatusAsync(wire);
        Assert.Equal(75, wireStatus!.OnHand);      // consumed 25
        Assert.Equal(0, wireStatus.CheckedOut);
        var drillStatus = await ledger.GetStatusAsync(drill);
        Assert.Equal(1, drillStatus!.OnHand);      // returned, unchanged
        Assert.Equal(0, drillStatus.CheckedOut);
        Assert.Equal(ProjectStatus.Completed, (await projects.GetAsync(pid))!.Status);
    }

    [Fact]
    public async Task Complete_With_Partial_Use_Returns_The_Remainder()
    {
        using var h = new TestHarness();
        var (projects, ledger) = Build(h);
        var wire = NewItem(h, "Wire", 100, "ft");
        var pid = (await projects.CreateAsync(new ProjectEditModel { Name = "P" })).Value;
        await projects.AddAllocationAsync(pid, wire, 25, true, null);
        var alloc = (await projects.GetAsync(pid))!.Allocations.Single();

        await projects.CompleteAsync(pid, new[] { new CloseoutLine(alloc.Id, 20) }); // used 20 of 25

        var status = await ledger.GetStatusAsync(wire);
        Assert.Equal(80, status!.OnHand);   // only 20 consumed; 5 returned
        Assert.Equal(0, status.CheckedOut);
    }

    [Fact]
    public async Task Cancel_Returns_Everything_Nothing_Consumed()
    {
        using var h = new TestHarness();
        var (projects, ledger) = Build(h);
        var wire = NewItem(h, "Wire", 100, "ft");
        var pid = (await projects.CreateAsync(new ProjectEditModel { Name = "P" })).Value;
        await projects.AddAllocationAsync(pid, wire, 40, true, null);

        Assert.True((await projects.CancelAsync(pid)).Succeeded);

        var status = await ledger.GetStatusAsync(wire);
        Assert.Equal(100, status!.OnHand);
        Assert.Equal(0, status.CheckedOut);
        Assert.Equal(ProjectStatus.Cancelled, (await projects.GetAsync(pid))!.Status);
    }

    [Fact]
    public async Task Removing_Allocation_Returns_Its_Reserved_Amount()
    {
        using var h = new TestHarness();
        var (projects, ledger) = Build(h);
        var wire = NewItem(h, "Wire", 100, "ft");
        var pid = (await projects.CreateAsync(new ProjectEditModel { Name = "P" })).Value;
        await projects.AddAllocationAsync(pid, wire, 30, true, null);
        var alloc = (await projects.GetAsync(pid))!.Allocations.Single();

        Assert.True((await projects.RemoveAllocationAsync(alloc.Id)).Succeeded);

        Assert.Equal(0, (await ledger.GetStatusAsync(wire))!.CheckedOut);
        Assert.Empty((await projects.GetAsync(pid))!.Allocations);
    }
}
