using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Xunit;

namespace HomeStock.Tests;

public class InventoryLedgerServiceTests
{
    private static (InventoryLedgerService svc, int itemId) Setup(TestHarness h, decimal qty = 100, string? unit = "ft")
    {
        var item = new InventoryItem { Name = "Wire", Quantity = qty, Unit = unit };
        h.Db.Items.Add(item);
        h.Db.SaveChanges();
        return (new InventoryLedgerService(h.Db, new FakeCurrentUser(), h.Logger<InventoryLedgerService>()), item.Id);
    }

    [Fact]
    public async Task CheckOut_Reduces_Available_Not_OnHand()
    {
        using var h = new TestHarness();
        var (svc, id) = Setup(h);

        Assert.True((await svc.CheckOutAsync(id, 25, "kitchen")).Succeeded);

        var s = await svc.GetStatusAsync(id);
        Assert.Equal(100, s!.OnHand);
        Assert.Equal(25, s.CheckedOut);
        Assert.Equal(75, s.Available);
    }

    [Fact]
    public async Task Cannot_CheckOut_More_Than_Available()
    {
        using var h = new TestHarness();
        var (svc, id) = Setup(h, qty: 10);

        var r = await svc.CheckOutAsync(id, 25, null);

        Assert.False(r.Succeeded);
        Assert.Contains(r.Errors, e => e.Contains("available"));
    }

    [Fact]
    public async Task Return_Restores_Available_Without_Changing_OnHand()
    {
        using var h = new TestHarness();
        var (svc, id) = Setup(h);
        await svc.CheckOutAsync(id, 25, null);

        Assert.True((await svc.ReturnAsync(id, 25, null)).Succeeded);

        var s = await svc.GetStatusAsync(id);
        Assert.Equal(100, s!.OnHand);
        Assert.Equal(0, s.CheckedOut);
        Assert.Equal(100, s.Available);
    }

    [Fact]
    public async Task Consume_Reduces_OnHand_And_Clears_The_Checked_Out_Portion()
    {
        using var h = new TestHarness();
        var (svc, id) = Setup(h);
        await svc.CheckOutAsync(id, 25, "project");   // took 25 out

        Assert.True((await svc.ConsumeAsync(id, 25, "used in wall")).Succeeded);

        var s = await svc.GetStatusAsync(id);
        Assert.Equal(75, s!.OnHand);       // 100 -> 75 consumed
        Assert.Equal(0, s.CheckedOut);     // the 25 that was out is now gone, not still "out"
        Assert.Equal(75, s.Available);
    }

    [Fact]
    public async Task Cannot_Consume_More_Than_OnHand()
    {
        using var h = new TestHarness();
        var (svc, id) = Setup(h, qty: 5);

        var r = await svc.ConsumeAsync(id, 10, null);

        Assert.False(r.Succeeded);
        Assert.Equal(5, (await svc.GetStatusAsync(id))!.OnHand);
    }

    [Fact]
    public async Task Restock_Increases_OnHand_And_Records_History()
    {
        using var h = new TestHarness();
        var (svc, id) = Setup(h, qty: 5);

        Assert.True((await svc.RestockAsync(id, 20, "bought a spool")).Succeeded);

        Assert.Equal(25, (await svc.GetStatusAsync(id))!.OnHand);
        Assert.Contains(h.Db.Transactions, t => t.Type == TransactionType.Restock && t.Quantity == 20);
        Assert.Contains(h.Db.ItemHistory, x => x.Summary!.Contains("Restocked"));
    }

    [Fact]
    public async Task Adjust_Sets_Exact_OnHand()
    {
        using var h = new TestHarness();
        var (svc, id) = Setup(h, qty: 100);

        Assert.True((await svc.AdjustAsync(id, 88, "recount")).Succeeded);

        Assert.Equal(88, (await svc.GetStatusAsync(id))!.OnHand);
    }

    [Fact]
    public async Task Partial_Return_And_Consume_Nets_Correctly()
    {
        using var h = new TestHarness();
        var (svc, id) = Setup(h);            // 100 on hand
        await svc.CheckOutAsync(id, 25, null);  // took 25
        await svc.ConsumeAsync(id, 10, null);   // used 10
        await svc.ReturnAsync(id, 15, null);    // put 15 back

        var s = await svc.GetStatusAsync(id);
        Assert.Equal(90, s!.OnHand);         // only the 10 used is gone
        Assert.Equal(0, s.CheckedOut);       // 25 - 15 returned - 10 consumed = 0
        Assert.Equal(90, s.Available);
    }
}
