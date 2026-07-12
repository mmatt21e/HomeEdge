using System.Text;
using HomeStock.Application.Models;
using HomeStock.Application.Services;
using HomeStock.Domain.Entities;
using Xunit;

namespace HomeStock.Tests;

public class ImportExportServiceTests
{
    private static ImportExportService NewService(TestHarness h)
    {
        var locations = new LocationService(h.Db, new FakeCodeGenerator(), h.Logger<LocationService>());
        var items = new ItemService(h.Db, locations, new FakeCurrentUser(), h.Logger<ItemService>());
        return new ImportExportService(h.Db, items, h.Logger<ImportExportService>());
    }

    private static MemoryStream Csv(string content) => new(Encoding.UTF8.GetBytes(content));

    private static readonly Dictionary<string, string> Mapping = new()
    {
        [ImportFields.Name] = "Name",
        [ImportFields.Barcode] = "Barcode",
        [ImportFields.Quantity] = "Qty",
        [ImportFields.Category] = "Category",
    };

    [Fact]
    public async Task Preview_Suggests_Mapping_From_Headers()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var csv = "Name,Model Number,Barcode\nDrill,DCD777,12345\n";

        var preview = await svc.PreviewCsvAsync(Csv(csv));

        Assert.True(preview.Succeeded);
        Assert.Equal(1, preview.Value!.TotalDataRows);
        Assert.Equal("Name", preview.Value.SuggestedMapping[ImportFields.Name]);
        Assert.Equal("Model Number", preview.Value.SuggestedMapping[ImportFields.ModelNumber]);
    }

    [Fact]
    public async Task Validate_Flags_Missing_Name_As_Error()
    {
        using var h = new TestHarness();
        var svc = NewService(h);
        var csv = "Name,Barcode,Qty,Category\nDrill,111,1,Tools\n,222,1,Tools\n";

        var result = await svc.ValidateCsvAsync(Csv(csv), Mapping);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.ValidCount);
        Assert.Equal(1, result.Value.InvalidCount);
    }

    [Fact]
    public async Task Validate_Detects_Duplicate_Barcode_In_File_And_Db()
    {
        using var h = new TestHarness();
        h.Db.Items.Add(new InventoryItem { Name = "Existing", Barcode = "999" });
        await h.Db.SaveChangesAsync();
        var svc = NewService(h);
        var csv = "Name,Barcode,Qty,Category\nA,999,1,Tools\nB,777,1,Tools\nC,777,1,Tools\n";

        var result = await svc.ValidateCsvAsync(Csv(csv), Mapping);

        // Row A duplicates the DB; the 2nd '777' duplicates within the file.
        Assert.Equal(2, result.Value!.DuplicateCount);
    }

    [Fact]
    public async Task Commit_Imports_Valid_Rows_And_Skips_Duplicates()
    {
        using var h = new TestHarness();
        h.Db.Categories.Add(new Category { Name = "Tools" });
        await h.Db.SaveChangesAsync();
        var svc = NewService(h);
        var csv = "Name,Barcode,Qty,Category\nDrill,111,2,Tools\nSaw,111,1,Tools\n"; // 2nd is dup barcode

        var result = await svc.CommitCsvAsync(Csv(csv), Mapping, skipDuplicates: true);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Imported);
        Assert.Equal(1, result.Value.Skipped);
        var imported = h.Db.Items.Single(i => i.Name == "Drill");
        Assert.Equal(2, imported.Quantity);
        Assert.NotNull(imported.CategoryId); // resolved "Tools" by name
    }

    [Fact]
    public async Task Export_Csv_RoundTrips_Back_Through_Import()
    {
        using var h = new TestHarness();
        h.Db.Items.Add(new InventoryItem { Name = "Router", Barcode = "abc", Quantity = 3 });
        await h.Db.SaveChangesAsync();
        var svc = NewService(h);

        var exported = await svc.ExportItemsCsvAsync(new ItemQuery());
        var text = Encoding.UTF8.GetString(exported.Content);

        Assert.Contains("Router", text);
        var preview = await svc.PreviewCsvAsync(new MemoryStream(exported.Content));
        Assert.True(preview.Succeeded);
        Assert.Equal(1, preview.Value!.TotalDataRows);
    }
}
