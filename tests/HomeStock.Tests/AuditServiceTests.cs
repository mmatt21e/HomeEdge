using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Xunit;

namespace HomeStock.Tests;

public class AuditServiceTests
{
    private static AuditService NewService(TestHarness h)
    {
        var locations = new LocationService(h.Db, new FakeCodeGenerator(), h.Logger<LocationService>());
        return new AuditService(h.Db, locations, new FakeCurrentUser(), h.Logger<AuditService>());
    }

    private static (int locationId, int itemA, int itemB) Seed(TestHarness h)
    {
        var loc = new Location { Name = "Garage", Code = "LOC-G1" };
        h.Db.Locations.Add(loc);
        h.Db.SaveChanges();
        var a = new InventoryItem { Name = "Drill", LocationId = loc.Id, Barcode = "111" };
        var b = new InventoryItem { Name = "Saw", LocationId = loc.Id };
        h.Db.Items.AddRange(a, b);
        h.Db.SaveChanges();
        return (loc.Id, a.Id, b.Id);
    }

    [Fact]
    public async Task Start_Snapshots_Expected_Items()
    {
        using var h = new TestHarness();
        var (locId, _, _) = Seed(h);
        var svc = NewService(h);

        var start = await svc.StartAsync(locId, includeSublocations: false);

        Assert.True(start.Succeeded);
        var audit = await svc.GetAsync(start.Value);
        Assert.Equal(2, audit!.Total);
        Assert.Equal(2, audit.Pending);
    }

    [Fact]
    public async Task ConfirmByScan_Marks_Matching_Item_Confirmed()
    {
        using var h = new TestHarness();
        var (locId, _, _) = Seed(h);
        var svc = NewService(h);
        var auditId = (await svc.StartAsync(locId, false)).Value;

        var r = await svc.ConfirmByScanAsync(auditId, "111");

        Assert.True(r.Succeeded);
        Assert.Equal(1, (await svc.GetAsync(auditId))!.Confirmed);
    }

    [Fact]
    public async Task Complete_Applies_Missing_And_Damaged_To_Items()
    {
        using var h = new TestHarness();
        var (locId, itemA, itemB) = Seed(h);
        var svc = NewService(h);
        var auditId = (await svc.StartAsync(locId, false)).Value;
        var audit = await svc.GetAsync(auditId);

        var rowA = audit!.Items.Single(i => i.ItemId == itemA);
        var rowB = audit.Items.Single(i => i.ItemId == itemB);
        await svc.RecordResultAsync(rowA.Id, AuditItemResult.Missing, null, null);
        await svc.RecordResultAsync(rowB.Id, AuditItemResult.Damaged, null, null);

        var done = await svc.CompleteAsync(auditId, "monthly check");

        Assert.True(done.Succeeded);
        Assert.Equal(ItemStatus.Missing, h.Db.Items.Single(i => i.Id == itemA).Status);
        Assert.Equal(ItemStatus.Damaged, h.Db.Items.Single(i => i.Id == itemB).Status);
        var completed = await svc.GetAsync(auditId);
        Assert.Equal(AuditStatus.Completed, completed!.Status);
        Assert.Equal(2, completed.Discrepancies.Count());
    }

    [Fact]
    public async Task Complete_Moved_Updates_Item_Location()
    {
        using var h = new TestHarness();
        var (locId, itemA, _) = Seed(h);
        var dest = new Location { Name = "Shed", Code = "LOC-S1" };
        h.Db.Locations.Add(dest);
        await h.Db.SaveChangesAsync();
        var svc = NewService(h);
        var auditId = (await svc.StartAsync(locId, false)).Value;
        var row = (await svc.GetAsync(auditId))!.Items.Single(i => i.ItemId == itemA);

        await svc.RecordResultAsync(row.Id, AuditItemResult.Moved, dest.Id, null);
        await svc.CompleteAsync(auditId, null);

        Assert.Equal(dest.Id, h.Db.Items.Single(i => i.Id == itemA).LocationId);
    }
}
