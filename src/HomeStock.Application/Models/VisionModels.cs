namespace HomeStock.Application.Models;

/// <summary>How the vision model should interpret the photo.</summary>
public enum VisionMode
{
    /// <summary>A scene (shelf, drawer, pile) that may contain several distinct items.</summary>
    Scene,
    /// <summary>A single product/box; extract one item's details in depth.</summary>
    SingleProduct
}

/// <summary>An item the vision model believes it identified in the photo.</summary>
public class DetectedItem
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? Brand { get; set; }
    public string? ModelNumber { get; set; }
    /// <summary>Model's confidence 0..1, if provided.</summary>
    public double? Confidence { get; set; }
}

/// <summary>Result of a vision extraction pass.</summary>
public record VisionExtractionResult(
    IReadOnlyList<DetectedItem> Items,
    string? ModelName,
    string? Notes);
