using HomeStock.Application.Models;
using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using Xunit;

namespace HomeStock.Tests;

public class CategoryServiceTests
{
    private static CategoryService NewService(TestHarness h) => new(h.Db, h.Logger<CategoryService>());

    [Fact]
    public async Task Create_Persists_And_Trims_Name()
    {
        using var h = new TestHarness();
        var svc = NewService(h);

        var result = await svc.CreateAsync(new CategoryEditModel { Name = "  Tools  " });

        Assert.True(result.Succeeded);
        var dto = await svc.GetAsync(result.Value);
        Assert.Equal("Tools", dto!.Name);
    }

    [Fact]
    public async Task Create_Rejects_Duplicate_Name()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        await svc.CreateAsync(new CategoryEditModel { Name = "Tools" });

        var dup = await svc.CreateAsync(new CategoryEditModel { Name = "Tools" });

        Assert.False(dup.Succeeded);
        Assert.Contains(dup.Errors, e => e.Contains("already exists"));
    }

    [Fact]
    public async Task Create_Rejects_Invalid_Color()
    {
        using var h = new TestHarness();
        var svc = NewService(h);

        var result = await svc.CreateAsync(new CategoryEditModel { Name = "Tools", Color = "not-a-color" });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_Blocked_When_Items_Use_Category()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var id = (await svc.CreateAsync(new CategoryEditModel { Name = "Tools" })).Value;
        h.Db.Items.Add(new InventoryItem { Name = "Drill", CategoryId = id });
        await h.Db.SaveChangesAsync();

        var result = await svc.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("still use this category"));
    }

    [Fact]
    public async Task System_Category_Cannot_Be_Deleted()
    {
        using var h = new TestHarness();
        h.Db.Categories.Add(new Category { Name = "Tools", IsSystem = true });
        await h.Db.SaveChangesAsync();
        var svc = NewService(h);
        var id = (await svc.GetAllAsync()).Single().Id;

        var result = await svc.DeleteAsync(id);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Archive_Then_Restore_Roundtrips()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var id = (await svc.CreateAsync(new CategoryEditModel { Name = "Seasonal" })).Value;

        await svc.ArchiveAsync(id);
        Assert.True((await svc.GetAsync(id))!.IsArchived);

        await svc.RestoreAsync(id);
        Assert.False((await svc.GetAsync(id))!.IsArchived);
    }
}
