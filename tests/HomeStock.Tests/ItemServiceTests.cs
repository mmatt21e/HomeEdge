using HomeStock.Application.Models;
using HomeStock.Application.Services;
using HomeStock.Domain.Enums;
using Xunit;

namespace HomeStock.Tests;

public class ItemServiceTests
{
    private static ItemService NewService(TestHarness h)
    {
        var locations = new LocationService(h.Db, new FakeCodeGenerator(), h.Logger<LocationService>());
        return new ItemService(h.Db, locations, new FakeCurrentUser(), h.Logger<ItemService>());
    }

    [Fact]
    public async Task Create_Writes_History_Entry()
    {
        using var h = new TestHarness();
        var svc = NewService(h);

        var result = await svc.CreateAsync(new ItemEditModel { Name = "Drill" });

        Assert.True(result.Succeeded);
        var history = h.Db.ItemHistory.Where(x => x.ItemId == result.Value).ToList();
        Assert.Single(history);
        Assert.Equal(HistoryAction.Created, history[0].Action);
        Assert.Equal("tester", history[0].UserName);
    }

    [Fact]
    public async Task Create_With_Tags_Creates_Join_Rows_And_Reuses_Tags()
    {
        using var h = new TestHarness();
        var svc = NewService(h);

        await svc.CreateAsync(new ItemEditModel { Name = "Drill", Tags = new() { "Power", "cordless" } });
        await svc.CreateAsync(new ItemEditModel { Name = "Saw", Tags = new() { "power" } }); // dup tag, different case

        Assert.Equal(2, h.Db.Tags.Count()); // "power" reused, not duplicated
    }

    [Fact]
    public async Task Duplicate_Barcode_Produces_Warning_Not_Error()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        await svc.CreateAsync(new ItemEditModel { Name = "Drill", Barcode = "12345" });

        var second = await svc.CreateAsync(new ItemEditModel { Name = "Other", Barcode = "12345" });

        Assert.True(second.Succeeded);
        Assert.Contains(second.Warnings, w => w.Contains("Barcode"));
    }

    [Fact]
    public async Task Update_Changing_Status_Records_StatusChanged_History()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var id = (await svc.CreateAsync(new ItemEditModel { Name = "Drill", Status = ItemStatus.Available })).Value;

        await svc.UpdateAsync(id, new ItemEditModel { Name = "Drill", Status = ItemStatus.Loaned });

        Assert.Contains(h.Db.ItemHistory, x => x.ItemId == id && x.Action == HistoryAction.StatusChanged);
    }

    [Fact]
    public async Task Search_Filters_By_Text_Across_Fields()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        await svc.CreateAsync(new ItemEditModel { Name = "Cordless Drill", Manufacturer = "DeWalt" });
        await svc.CreateAsync(new ItemEditModel { Name = "Router", Manufacturer = "TP-Link" });

        var byName = await svc.SearchAsync(new ItemQuery { Search = "drill" });
        var byManufacturer = await svc.SearchAsync(new ItemQuery { Search = "TP-Link" });

        Assert.Single(byName.Items);
        Assert.Equal("Cordless Drill", byName.Items[0].Name);
        Assert.Single(byManufacturer.Items);
    }

    [Fact]
    public async Task Archive_Excludes_From_Default_Search_But_Restore_Brings_Back()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var id = (await svc.CreateAsync(new ItemEditModel { Name = "Drill" })).Value;

        await svc.ArchiveAsync(id);
        Assert.Empty((await svc.SearchAsync(new ItemQuery())).Items);
        Assert.Single((await svc.SearchAsync(new ItemQuery { OnlyArchived = true })).Items);

        await svc.RestoreAsync(id);
        Assert.Single((await svc.SearchAsync(new ItemQuery())).Items);
    }

    [Fact]
    public async Task Search_MissingLocation_Filter_Works()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        await svc.CreateAsync(new ItemEditModel { Name = "No location item" });

        var result = await svc.SearchAsync(new ItemQuery { MissingLocation = true });

        Assert.Single(result.Items);
    }
}
