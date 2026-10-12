using Unayka.Core.Locations;

namespace Unayka.Core.Catalog;

/// <summary>Item for sale. Prices are in minor units (centavos of MZN). Physical stock lives in LocationStock.</summary>
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
    public ProductKind Kind { get; set; }
    public string? Badge { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public int SalesCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ProductMedia> Media { get; set; } = [];
    public List<ProductFact> Facts { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
    public List<LocationStock> Stocks { get; set; } = [];
    public List<DigitalAsset> DigitalAssets { get; set; } = [];
}
