using System.Text;
using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using HomeStock.Domain.Enums;
using Xunit;

namespace HomeStock.Tests;

public class AttachmentServiceTests
{
    private static (AttachmentService svc, InventoryItem item) Setup(TestHarness h, TempFileStorage storage)
    {
        var item = new InventoryItem { Name = "Drill" };
        h.Db.Items.Add(item);
        h.Db.SaveChanges();
        var svc = new AttachmentService(h.Db, storage.Service, new FakeCurrentUser(), h.Logger<AttachmentService>());
        return (svc, item);
    }

    [Fact]
    public async Task Add_Stores_File_And_Records_History()
    {
        using var h = new TestHarness();
        using var storage = new TempFileStorage();
        var (svc, item) = Setup(h, storage);

        var bytes = Encoding.UTF8.GetBytes("fake-jpeg-bytes");
        var result = await svc.AddAsync(item.Id, new MemoryStream(bytes), "receipt.jpg", "image/jpeg", AttachmentType.Receipt, "Store receipt");

        Assert.True(result.Succeeded);
        var list = await svc.GetForItemAsync(item.Id);
        Assert.Single(list);
        Assert.Equal(AttachmentType.Receipt, list[0].Type);
        Assert.Equal(bytes.Length, list[0].SizeBytes);
        Assert.Contains(h.Db.ItemHistory, x => x.Action == HistoryAction.AttachmentAdded);
    }

    [Fact]
    public async Task Add_Rejects_Disallowed_Extension()
    {
        using var h = new TestHarness();
        using var storage = new TempFileStorage();
        var (svc, item) = Setup(h, storage);

        var result = await svc.AddAsync(item.Id, new MemoryStream(new byte[] { 1, 2, 3 }), "malware.exe", "application/octet-stream", AttachmentType.Other, null);

        Assert.False(result.Succeeded);
        Assert.Empty(await svc.GetForItemAsync(item.Id));
    }

    [Fact]
    public async Task Open_Returns_Stored_Content_Then_Delete_Removes_It()
    {
        using var h = new TestHarness();
        using var storage = new TempFileStorage();
        var (svc, item) = Setup(h, storage);
        var id = (await svc.AddAsync(item.Id, new MemoryStream(Encoding.UTF8.GetBytes("hello")), "manual.pdf", "application/pdf", AttachmentType.Manual, null)).Value;

        var opened = await svc.OpenAsync(id);
        Assert.NotNull(opened);
        using (var reader = new StreamReader(opened!.Content)) Assert.Equal("hello", await reader.ReadToEndAsync());

        Assert.True((await svc.DeleteAsync(id)).Succeeded);
        Assert.Null(await svc.OpenAsync(id));
    }
}
