using HomeStock.Application.Models;
using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Xunit;

namespace HomeStock.Tests;

public class LoanServiceTests
{
    private static (LoanService svc, int itemId) Setup(TestHarness h)
    {
        var item = new InventoryItem { Name = "Ladder", Status = ItemStatus.Available };
        h.Db.Items.Add(item);
        h.Db.SaveChanges();
        return (new LoanService(h.Db, new FakeCurrentUser(), h.Logger<LoanService>()), item.Id);
    }

    [Fact]
    public async Task LoanOut_Sets_Status_And_Records_History()
    {
        using var h = new TestHarness();
        var (svc, itemId) = Setup(h);

        var r = await svc.LoanOutAsync(itemId, new LoanEditModel { BorrowerName = "Alex" });

        Assert.True(r.Succeeded);
        Assert.Equal(ItemStatus.Loaned, h.Db.Items.Single(i => i.Id == itemId).Status);
        Assert.Contains(h.Db.ItemHistory, x => x.Action == HistoryAction.Loaned);
        Assert.NotNull(await svc.GetOpenLoanAsync(itemId));
    }

    [Fact]
    public async Task Cannot_Loan_An_Already_Loaned_Item()
    {
        using var h = new TestHarness();
        var (svc, itemId) = Setup(h);
        await svc.LoanOutAsync(itemId, new LoanEditModel { BorrowerName = "Alex" });

        var second = await svc.LoanOutAsync(itemId, new LoanEditModel { BorrowerName = "Sam" });

        Assert.False(second.Succeeded);
    }

    [Fact]
    public async Task Return_Stamps_Date_And_Restores_Status()
    {
        using var h = new TestHarness();
        var (svc, itemId) = Setup(h);
        var loanId = (await svc.LoanOutAsync(itemId, new LoanEditModel { BorrowerName = "Alex" })).Value;

        var r = await svc.ReturnAsync(loanId, null, "good condition");

        Assert.True(r.Succeeded);
        Assert.Equal(ItemStatus.Available, h.Db.Items.Single(i => i.Id == itemId).Status);
        Assert.Null(await svc.GetOpenLoanAsync(itemId));
        Assert.Contains(h.Db.ItemHistory, x => x.Action == HistoryAction.Returned);
    }

    [Fact]
    public async Task Open_Loans_Sorts_Overdue_First()
    {
        using var h = new TestHarness();
        var (svc, itemId) = Setup(h);
        var item2 = new InventoryItem { Name = "Drill" };
        h.Db.Items.Add(item2);
        await h.Db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await svc.LoanOutAsync(itemId, new LoanEditModel { BorrowerName = "OnTime", ExpectedReturnDate = today.AddDays(30) });
        // Loaned 10 days ago, was due 2 days ago -> overdue (valid: due is after the loan date).
        await svc.LoanOutAsync(item2.Id, new LoanEditModel { BorrowerName = "Late", LoanDate = today.AddDays(-10), ExpectedReturnDate = today.AddDays(-2) });

        var open = await svc.GetOpenLoansAsync();
        Assert.Equal(2, open.Count);
        Assert.True(open[0].IsOverdue);
        Assert.Equal("Late", open[0].BorrowerName);
    }
}
