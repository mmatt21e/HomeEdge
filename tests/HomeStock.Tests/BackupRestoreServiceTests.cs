using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Xunit;

namespace HomeStock.Tests;

public class BackupRestoreServiceTests
{
    private static async Task<byte[]> MakeBackup()
    {
        // Build a source database, then export a JSON backup from it.
        using var src = new TestHarness();
        var cat = new Category { Name = "Tools" };
        var loc = new Location { Name = "Garage", Code = "LOC-BK1" };
        var child = new Location { Name = "Cabinet", Code = "LOC-BK2", Parent = loc };
        src.Db.Categories.Add(cat);
        src.Db.Locations.AddRange(loc, child);
        await src.Db.SaveChangesAsync();
        src.Db.Items.Add(new InventoryItem
        {
            Name = "Drill", Category = cat, Location = child, Barcode = "abc", SerialNumber = "SN1",
            Quantity = 2, Condition = ItemCondition.Good, Status = ItemStatus.Available,
            ItemTags = new List<ItemTag> { new() { Tag = new Tag { Name = "power", NormalizedName = "power" } } }
        });
        await src.Db.SaveChangesAsync();

        var exporter = new ImportExportService(src.Db,
            new ItemService(src.Db, new LocationService(src.Db, new FakeCodeGenerator(), src.Logger<LocationService>()), new FakeCurrentUser(), src.Logger<ItemService>()),
            src.Logger<ImportExportService>());
        var file = await exporter.ExportJsonBackupAsync();
        return file.Content;
    }

    private static BackupRestoreService NewService(TestHarness h)
        => new(h.Db, new FakeCodeGenerator(), h.Logger<BackupRestoreService>());

    [Fact]
    public async Task Validate_Reports_Counts()
    {
        var backup = await MakeBackup();
        using var h = new TestHarness();
        var svc = NewService(h);

        var result = await svc.ValidateAsync(new MemoryStream(backup));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.ItemCount);
        Assert.Equal(1, result.Value.CategoryCount);
        Assert.Equal(2, result.Value.LocationCount);
        Assert.False(result.Value.CurrentDatabaseHasItems);
    }

    [Fact]
    public async Task Restore_Into_Empty_Db_Recreates_Hierarchy_And_Item()
    {
        var backup = await MakeBackup();
        using var h = new TestHarness();
        var svc = NewService(h);

        var result = await svc.RestoreAsync(new MemoryStream(backup));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.ItemsAdded);
        Assert.Equal(2, result.Value.LocationsAdded);

        var item = h.Db.Items.Single();
        Assert.Equal("Drill", item.Name);
        Assert.Equal(2, item.Quantity);
        Assert.NotNull(item.CategoryId);
        Assert.NotNull(item.LocationId);
        // Location hierarchy preserved: the item's location has a parent.
        var loc = h.Db.Locations.Single(l => l.Id == item.LocationId);
        Assert.NotNull(loc.ParentId);
    }

    [Fact]
    public async Task Restore_Is_Additive_And_Skips_Existing_Items()
    {
        var backup = await MakeBackup();
        using var h = new TestHarness();
        var svc = NewService(h);

        await svc.RestoreAsync(new MemoryStream(backup));
        var second = await svc.RestoreAsync(new MemoryStream(backup)); // same file again

        Assert.Equal(0, second.Value!.ItemsAdded);
        Assert.Equal(1, second.Value.ItemsSkipped);
        Assert.Single(h.Db.Items); // no duplicate created
    }
}
