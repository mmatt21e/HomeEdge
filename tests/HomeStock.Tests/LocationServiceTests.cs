using HomeStock.Application.Models;
using HomeStock.Application.Services;
using Xunit;

namespace HomeStock.Tests;

public class LocationServiceTests
{
    private static LocationService NewService(TestHarness h) =>
        new(h.Db, new FakeCodeGenerator(), h.Logger<LocationService>());

    [Fact]
    public async Task Create_Assigns_Unique_Code()
    {
        using var h = new TestHarness();
        var svc = NewService(h);

        var a = await svc.CreateAsync(new LocationEditModel { Name = "Garage" });
        var b = await svc.CreateAsync(new LocationEditModel { Name = "Basement" });

        var codeA = (await svc.GetAsync(a.Value))!.Code;
        var codeB = (await svc.GetAsync(b.Value))!.Code;
        Assert.False(string.IsNullOrWhiteSpace(codeA));
        Assert.NotEqual(codeA, codeB);
    }

    [Fact]
    public async Task Tree_Is_Built_Depth_First_With_Paths()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var home = (await svc.CreateAsync(new LocationEditModel { Name = "Home" })).Value;
        var garage = (await svc.CreateAsync(new LocationEditModel { Name = "Garage", ParentId = home })).Value;
        await svc.CreateAsync(new LocationEditModel { Name = "Cabinet", ParentId = garage });

        var tree = await svc.GetTreeAsync();

        Assert.Equal(3, tree.Count);
        Assert.Equal(0, tree[0].Depth);
        Assert.Equal("Home / Garage / Cabinet", tree[2].Path);
        Assert.Equal(2, tree[2].Depth);
    }

    [Fact]
    public async Task Move_Into_Own_Descendant_Is_Rejected()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var home = (await svc.CreateAsync(new LocationEditModel { Name = "Home" })).Value;
        var garage = (await svc.CreateAsync(new LocationEditModel { Name = "Garage", ParentId = home })).Value;

        var result = await svc.MoveAsync(home, garage);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("descendant"));
    }

    [Fact]
    public async Task SelfAndDescendantIds_Returns_Whole_Subtree()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var home = (await svc.CreateAsync(new LocationEditModel { Name = "Home" })).Value;
        var garage = (await svc.CreateAsync(new LocationEditModel { Name = "Garage", ParentId = home })).Value;
        var cabinet = (await svc.CreateAsync(new LocationEditModel { Name = "Cabinet", ParentId = garage })).Value;

        var ids = await svc.GetSelfAndDescendantIdsAsync(home);

        Assert.Equal(new[] { home, garage, cabinet }.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public async Task Delete_Blocked_When_Has_Children()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var home = (await svc.CreateAsync(new LocationEditModel { Name = "Home" })).Value;
        await svc.CreateAsync(new LocationEditModel { Name = "Garage", ParentId = home });

        var result = await svc.DeleteAsync(home);

        Assert.False(result.Succeeded);
    }
}
