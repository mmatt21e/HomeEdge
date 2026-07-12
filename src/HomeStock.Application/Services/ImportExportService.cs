using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeStock.Application.Services;

public class ImportExportService(
    IApplicationDbContext db,
    IItemService items,
    ILogger<ImportExportService> logger) : IImportExportService
{
    private static readonly string[] DateFormats =
        { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yyyy", "yyyy/MM/dd", "dd-MM-yyyy", "yyyyMMdd" };

    // ---------------- Export ----------------

    public async Task<ExportFile> ExportItemsCsvAsync(ItemQuery query, CancellationToken ct = default)
    {
        // Pull the full matching set (bypass paging) via a large page size.
        query.Page = 1;
        query.PageSize = 200;
        var rows = new List<ItemDto>();
        while (true)
        {
            var page = await items.SearchAsync(query, ct);
            foreach (var listItem in page.Items)
            {
                var dto = await items.GetAsync(listItem.Id, ct);
                if (dto is not null) rows.Add(dto);
            }
            if (!page.HasNext) break;
            query.Page++;
        }

        using var buffer = new MemoryStream();
        await using (var writer = new StreamWriter(buffer, new UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var h in ImportFields.All) csv.WriteField(h);
            await csv.NextRecordAsync();

            foreach (var i in rows)
            {
                csv.WriteField(i.Name);
                csv.WriteField(i.Description);
                csv.WriteField(i.CategoryName);
                csv.WriteField(i.Subcategory);
                csv.WriteField(i.Quantity);
                csv.WriteField(i.Unit);
                csv.WriteField(i.Manufacturer);
                csv.WriteField(i.Brand);
                csv.WriteField(i.ModelNumber);
                csv.WriteField(i.SerialNumber);
                csv.WriteField(i.Barcode);
                csv.WriteField(i.PurchaseDate?.ToString("yyyy-MM-dd"));
                csv.WriteField(i.PurchaseLocation);
                csv.WriteField(i.PurchasePrice);
                csv.WriteField(i.EstimatedValue);
                csv.WriteField(i.Condition);
                csv.WriteField(i.WarrantyExpiration?.ToString("yyyy-MM-dd"));
                csv.WriteField(i.LocationName);
                csv.WriteField(i.Container);
                csv.WriteField(i.Status);
                csv.WriteField(string.Join("; ", i.Tags));
                csv.WriteField(i.Notes);
                await csv.NextRecordAsync();
            }
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return new ExportFile(buffer.ToArray(), "text/csv", $"homestock-items-{stamp}.csv");
    }

    public async Task<ExportFile> ExportJsonBackupAsync(CancellationToken ct = default)
    {
        var backup = new
        {
            exportedAtUtc = DateTime.UtcNow,
            schema = 1,
            categories = await db.Categories.AsNoTracking()
                .Select(c => new { c.Id, c.Name, c.Description, c.Color, c.Icon, c.IsSystem, c.IsArchived }).ToListAsync(ct),
            locations = await db.Locations.AsNoTracking()
                .Select(l => new { l.Id, l.Name, l.Description, l.ParentId, l.Code, l.IsArchived }).ToListAsync(ct),
            tags = await db.Tags.AsNoTracking().Select(t => new { t.Id, t.Name }).ToListAsync(ct),
            items = await db.Items.AsNoTracking().Select(i => new
            {
                i.Id, i.Name, i.Description, i.Notes, i.CategoryId, i.Subcategory, i.Quantity, i.Unit,
                i.Manufacturer, i.Brand, i.ModelNumber, i.SerialNumber, i.Barcode,
                i.PurchaseDate, i.PurchaseLocation, i.PurchasePrice, i.EstimatedValue,
                Condition = i.Condition.ToString(), i.WarrantyExpiration, i.LocationId, i.Container,
                Status = i.Status.ToString(), i.IsArchived, i.CreatedAt, i.UpdatedAt,
                Tags = i.ItemTags.Select(t => t.Tag.Name).ToList()
            }).ToListAsync(ct)
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(backup, new JsonSerializerOptions { WriteIndented = true });
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return new ExportFile(json, "application/json", $"homestock-backup-{stamp}.json");
    }

    // ---------------- Import ----------------

    public async Task<Result<ImportPreview>> PreviewCsvAsync(Stream csv, CancellationToken ct = default)
    {
        var rows = await ReadAllRowsAsync(csv, ct);
        if (rows.Count == 0) return Result<ImportPreview>.Failure("The file is empty.");

        var headers = rows[0];
        if (headers.Count == 0) return Result<ImportPreview>.Failure("No header row found.");

        var sample = rows.Skip(1).Take(5).ToList<IReadOnlyList<string>>();
        var suggested = SuggestMapping(headers);
        return Result<ImportPreview>.Success(new ImportPreview(headers, sample, rows.Count - 1, suggested));
    }

    public async Task<Result<ImportValidation>> ValidateCsvAsync(Stream csv, IReadOnlyDictionary<string, string> mapping, CancellationToken ct = default)
    {
        var rows = await ReadAllRowsAsync(csv, ct);
        if (rows.Count <= 1) return Result<ImportValidation>.Failure("No data rows to import.");
        if (!mapping.ContainsKey(ImportFields.Name))
            return Result<ImportValidation>.Failure("The Name field must be mapped to a column.");

        var header = rows[0];
        var index = BuildColumnIndex(header, mapping);

        // Reference/dup lookups.
        var categoryNames = await db.Categories.AsNoTracking().Select(c => c.Name).ToListAsync(ct);
        var locationNames = await db.Locations.AsNoTracking().Select(l => l.Name).ToListAsync(ct);
        var existingBarcodes = await db.Items.AsNoTracking().Where(i => i.Barcode != null).Select(i => i.Barcode!).ToListAsync(ct);
        var existingSerials = await db.Items.AsNoTracking().Where(i => i.SerialNumber != null).Select(i => i.SerialNumber!).ToListAsync(ct);
        var barcodeSet = new HashSet<string>(existingBarcodes, StringComparer.OrdinalIgnoreCase);
        var serialSet = new HashSet<string>(existingSerials, StringComparer.OrdinalIgnoreCase);
        var catSet = new HashSet<string>(categoryNames, StringComparer.OrdinalIgnoreCase);
        var locSet = new HashSet<string>(locationNames, StringComparer.OrdinalIgnoreCase);

        var seenBarcodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSerialsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var results = new List<ImportRowResult>();
        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var (model, parseErrors, catName, locName) = BuildModel(row, index);
            var errors = new List<string>(parseErrors);
            var warnings = new List<string>();

            errors.AddRange(ModelValidator.Validate(model));

            if (!string.IsNullOrWhiteSpace(catName) && !catSet.Contains(catName))
                warnings.Add($"Category '{catName}' not found — item will import without a category.");
            if (!string.IsNullOrWhiteSpace(locName) && !locSet.Contains(locName))
                warnings.Add($"Location '{locName}' not found — item will import without a location.");

            var isDup = false;
            if (!string.IsNullOrWhiteSpace(model.Barcode))
            {
                if (barcodeSet.Contains(model.Barcode!) || !seenBarcodesInFile.Add(model.Barcode!))
                { isDup = true; warnings.Add($"Duplicate barcode '{model.Barcode}'."); }
            }
            if (!string.IsNullOrWhiteSpace(model.SerialNumber))
            {
                if (serialSet.Contains(model.SerialNumber!) || !seenSerialsInFile.Add(model.SerialNumber!))
                { isDup = true; warnings.Add($"Duplicate serial number '{model.SerialNumber}'."); }
            }

            var willImport = errors.Count == 0;
            results.Add(new ImportRowResult(r + 1, model.Name, errors, warnings, isDup, willImport));
        }

        var valid = results.Count(x => x.WillImport);
        var invalid = results.Count(x => !x.WillImport);
        var dups = results.Count(x => x.IsDuplicate);
        return Result<ImportValidation>.Success(new ImportValidation(results, valid, invalid, dups));
    }

    public async Task<Result<ImportCommitResult>> CommitCsvAsync(Stream csv, IReadOnlyDictionary<string, string> mapping, bool skipDuplicates, CancellationToken ct = default)
    {
        var rows = await ReadAllRowsAsync(csv, ct);
        if (rows.Count <= 1) return Result<ImportCommitResult>.Failure("No data rows to import.");
        if (!mapping.ContainsKey(ImportFields.Name))
            return Result<ImportCommitResult>.Failure("The Name field must be mapped to a column.");

        var header = rows[0];
        var index = BuildColumnIndex(header, mapping);

        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var locations = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Name, l => l.Id, StringComparer.OrdinalIgnoreCase, ct);

        int imported = 0, skipped = 0;
        var errors = new List<string>();
        var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var r = 1; r < rows.Count; r++)
        {
            var (model, parseErrors, catName, locName) = BuildModel(rows[r], index);
            if (parseErrors.Count > 0 || !ModelValidator.TryValidate(model, out _)) { skipped++; continue; }

            if (skipDuplicates && IsDuplicate(model, seenBarcodes, seenSerials))
            {
                skipped++;
                continue;
            }
            if (!string.IsNullOrWhiteSpace(model.Barcode)) seenBarcodes.Add(model.Barcode!);
            if (!string.IsNullOrWhiteSpace(model.SerialNumber)) seenSerials.Add(model.SerialNumber!);

            if (!string.IsNullOrWhiteSpace(catName) && categories.TryGetValue(catName, out var cid)) model.CategoryId = cid;
            if (!string.IsNullOrWhiteSpace(locName) && locations.TryGetValue(locName, out var lid)) model.LocationId = lid;

            var result = await items.CreateAsync(model, ct);
            if (result.Succeeded) imported++;
            else { skipped++; errors.Add($"Row {r + 1}: {string.Join(" ", result.Errors)}"); }
        }

        logger.LogInformation("CSV import committed: {Imported} imported, {Skipped} skipped", imported, skipped);
        return Result<ImportCommitResult>.Success(new ImportCommitResult(imported, skipped, errors));
    }

    // ---------------- Helpers ----------------

    private bool IsDuplicate(ItemEditModel model, HashSet<string> seenBarcodes, HashSet<string> seenSerials)
    {
        if (!string.IsNullOrWhiteSpace(model.Barcode) &&
            (seenBarcodes.Contains(model.Barcode!) || db.Items.Any(i => i.Barcode == model.Barcode))) return true;
        if (!string.IsNullOrWhiteSpace(model.SerialNumber) &&
            (seenSerials.Contains(model.SerialNumber!) || db.Items.Any(i => i.SerialNumber == model.SerialNumber))) return true;
        return false;
    }

    private static async Task<List<IReadOnlyList<string>>> ReadAllRowsAsync(Stream csv, CancellationToken ct)
    {
        if (csv.CanSeek) csv.Position = 0;
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false, DetectDelimiter = true, BadDataFound = null };
        using var reader = new StreamReader(csv, leaveOpen: true);
        using var parser = new CsvParser(reader, config);
        var rows = new List<IReadOnlyList<string>>();
        while (await parser.ReadAsync())
        {
            if (ct.IsCancellationRequested) break;
            rows.Add(parser.Record ?? Array.Empty<string>());
        }
        return rows;
    }

    private static Dictionary<string, int> BuildColumnIndex(IReadOnlyList<string> header, IReadOnlyDictionary<string, string> mapping)
    {
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++) headerIndex[header[i]] = i;

        var index = new Dictionary<string, int>();
        foreach (var (field, headerName) in mapping)
            if (headerIndex.TryGetValue(headerName, out var col)) index[field] = col;
        return index;
    }

    private static (ItemEditModel model, List<string> errors, string? categoryName, string? locationName)
        BuildModel(IReadOnlyList<string> row, Dictionary<string, int> index)
    {
        var errors = new List<string>();
        string? Cell(string field) => index.TryGetValue(field, out var c) && c < row.Count ? row[c]?.Trim() : null;

        var model = new ItemEditModel
        {
            Name = Cell(ImportFields.Name) ?? "",
            Description = Cell(ImportFields.Description),
            Subcategory = Cell(ImportFields.Subcategory),
            Manufacturer = Cell(ImportFields.Manufacturer),
            Brand = Cell(ImportFields.Brand),
            ModelNumber = Cell(ImportFields.ModelNumber),
            SerialNumber = Cell(ImportFields.SerialNumber),
            Barcode = Cell(ImportFields.Barcode),
            PurchaseLocation = Cell(ImportFields.PurchaseLocation),
            Container = Cell(ImportFields.Container),
            Unit = Cell(ImportFields.Unit),
            Notes = Cell(ImportFields.Notes),
        };

        var qty = Cell(ImportFields.Quantity);
        if (!string.IsNullOrWhiteSpace(qty))
        {
            if (decimal.TryParse(qty, NumberStyles.Number, CultureInfo.InvariantCulture, out var q)) model.Quantity = q;
            else errors.Add($"Quantity '{qty}' is not a number.");
        }

        model.PurchasePrice = ParseDecimal(Cell(ImportFields.PurchasePrice), "Purchase price", errors);
        model.EstimatedValue = ParseDecimal(Cell(ImportFields.EstimatedValue), "Estimated value", errors);
        model.PurchaseDate = ParseDate(Cell(ImportFields.PurchaseDate), "Purchase date", errors);
        model.WarrantyExpiration = ParseDate(Cell(ImportFields.WarrantyExpiration), "Warranty expiration", errors);

        var cond = Cell(ImportFields.Condition);
        if (!string.IsNullOrWhiteSpace(cond))
        {
            if (TryParseEnum<ItemCondition>(cond, out var c)) model.Condition = c;
            else errors.Add($"Condition '{cond}' is not recognised.");
        }

        var status = Cell(ImportFields.Status);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (TryParseEnum<ItemStatus>(status, out var s)) model.Status = s;
            else errors.Add($"Status '{status}' is not recognised.");
        }

        var tags = Cell(ImportFields.Tags);
        if (!string.IsNullOrWhiteSpace(tags))
            model.Tags = tags.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return (model, errors, Cell(ImportFields.Category), Cell(ImportFields.Location));
    }

    private static decimal? ParseDecimal(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Replace("$", "").Replace(",", "").Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
        errors.Add($"{label} '{value}' is not a valid amount.");
        return null;
    }

    private static DateOnly? ParseDate(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
        errors.Add($"{label} '{value}' is not a valid date (try yyyy-MM-dd).");
        return null;
    }

    private static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
    {
        var normalized = value.Replace(" ", "").Replace("-", "").Replace("/", "");
        return Enum.TryParse(normalized, ignoreCase: true, out result);
    }

    private static Dictionary<string, string> SuggestMapping(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, string>();
        foreach (var field in ImportFields.All)
        {
            var fieldNorm = Normalize(field);
            var match = headers.FirstOrDefault(h => Normalize(h) == fieldNorm)
                        ?? headers.FirstOrDefault(h => Normalize(h).Contains(fieldNorm) || fieldNorm.Contains(Normalize(h)));
            if (match is not null && !map.ContainsValue(match)) map[field] = match;
        }
        return map;
    }

    private static string Normalize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
