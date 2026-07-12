using HomeStock.Application.Models;
using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using HomeStock.Web.Infrastructure;
using Xunit;

namespace HomeStock.Tests;

public class DashboardServiceTests
{
    [Fact]
    public async Task Aggregates_Totals_Value_And_Breakdowns()
    {
        using var h = new TestHarness();
        var cat = new Category { Name = "Tools" };
        h.Db.Categories.Add(cat);
        h.Db.Items.AddRange(
            new InventoryItem { Name = "Drill", Category = cat, Quantity = 2, EstimatedValue = 50m, Status = ItemStatus.Loaned },
            new InventoryItem { Name = "Saw", Category = cat, Quantity = 1, EstimatedValue = 30m },
            new InventoryItem { Name = "Archived", IsArchived = true, EstimatedValue = 999m });
        await h.Db.SaveChangesAsync();

        var dto = await new DashboardService(h.Db).GetAsync();

        Assert.Equal(2, dto.TotalItemRecords);          // archived excluded
        Assert.Equal(3, dto.TotalQuantity);
        Assert.Equal(80m, dto.EstimatedTotalValue);     // archived value excluded
        Assert.Equal(1, dto.LoanedOutCount);
        Assert.Contains(dto.CountByCategory, c => c.Name == "Tools" && c.Count == 2);
    }
}

public class DisplayHelpersTests
{
    [Theory]
    [InlineData(0, WarrantyState.None)]     // no date
    public void Warranty_None_When_No_Date(int _, WarrantyState expected)
        => Assert.Equal(expected, DisplayHelpers.WarrantyStateOf(null));

    [Fact]
    public void Warranty_Expired_For_Past_Date()
    {
        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        Assert.Equal(WarrantyState.Expired, DisplayHelpers.WarrantyStateOf(past));
    }

    [Fact]
    public void Warranty_ExpiringSoon_Within_Window()
    {
        var soon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
        Assert.Equal(WarrantyState.ExpiringSoon, DisplayHelpers.WarrantyStateOf(soon, 30));
    }

    [Fact]
    public void Warranty_Active_Beyond_Window()
    {
        var far = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(200);
        Assert.Equal(WarrantyState.Active, DisplayHelpers.WarrantyStateOf(far, 30));
    }

    [Fact]
    public void Status_Labels_Are_Human_Readable()
    {
        Assert.Equal("In use", DisplayHelpers.Label(ItemStatus.InUse));
        Assert.Equal("Like new", DisplayHelpers.Label(ItemCondition.LikeNew));
    }
}
