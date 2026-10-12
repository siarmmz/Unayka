namespace Unayka.Core.Catalog;

/// <summary>Item for sale. Prices are in minor units (centavos of MZN).</summary>
public sealed class Product
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long PriceMinor { get; set; }
    public long? CompareAtPriceMinor { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? Badge { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int StockQuantity { get; set; }
    public int SalesCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ProductMedia> Media { get; set; } = [];
    public List<ProductFact> Facts { get; set; } = [];
}
