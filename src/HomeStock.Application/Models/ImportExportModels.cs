namespace HomeStock.Application.Models;

/// <summary>The item fields that can be targeted by a CSV import column mapping.</summary>
public static class ImportFields
{
    public const string Name = "Name";
    public const string Description = "Description";
    public const string Category = "Category";
    public const string Subcategory = "Subcategory";
    public const string Quantity = "Quantity";
    public const string Manufacturer = "Manufacturer";
    public const string Brand = "Brand";
    public const string ModelNumber = "ModelNumber";
    public const string SerialNumber = "SerialNumber";
    public const string Barcode = "Barcode";
    public const string PurchaseDate = "PurchaseDate";
    public const string PurchaseLocation = "PurchaseLocation";
    public const string PurchasePrice = "PurchasePrice";
    public const string EstimatedValue = "EstimatedValue";
    public const string Condition = "Condition";
    public const string WarrantyExpiration = "WarrantyExpiration";
    public const string Location = "Location";
    public const string Container = "Container";
    public const string Status = "Status";
    public const string Tags = "Tags";
    public const string Notes = "Notes";

    /// <summary>All mappable fields in a sensible display order.</summary>
    public static readonly string[] All =
    {
        Name, Description, Category, Subcategory, Quantity, Manufacturer, Brand, ModelNumber,
        SerialNumber, Barcode, PurchaseDate, PurchaseLocation, PurchasePrice, EstimatedValue,
        Condition, WarrantyExpiration, Location, Container, Status, Tags, Notes
    };
}

/// <summary>Result of reading a CSV: its headers, a few sample rows, and an auto-detected mapping.</summary>
public record ImportPreview(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> SampleRows,
    int TotalDataRows,
    IReadOnlyDictionary<string, string> SuggestedMapping); // field -> header

/// <summary>One validated row from an import, with the values that would be applied.</summary>
public record ImportRowResult(
    int RowNumber,
    string Name,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    bool IsDuplicate,
    bool WillImport);

public record ImportValidation(
    IReadOnlyList<ImportRowResult> Rows,
    int ValidCount,
    int InvalidCount,
    int DuplicateCount)
{
    public bool HasImportableRows => ValidCount > 0;
}

public record ImportCommitResult(int Imported, int Skipped, IReadOnlyList<string> Errors);

/// <summary>A serialised export payload with content type and suggested filename.</summary>
public record ExportFile(byte[] Content, string ContentType, string FileName);
