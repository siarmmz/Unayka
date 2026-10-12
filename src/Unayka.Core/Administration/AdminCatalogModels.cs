using Unayka.Core.Catalog;

namespace Unayka.Core.Administration;

/// <summary>Subpage of the product list.</summary>
public enum ProductListView
{
    All,
    New,
    Featured,
    Inactive,
    Archived,
}

/// <summary>Row of the product list. CanDelete is false when an order references the product.</summary>
public sealed record ProductRow(
    int Id,
    string Name,
    string Slug,
    string CategoryName,
    long PriceMinor,
    long? CompareAtPriceMinor,
    int StockQuantity,
    CatalogStatus Status,
    bool IsFeatured,
    bool IsNew,
    ProductKind Kind,
    string? ImageUrl,
    bool CanDelete);

/// <summary>Category choice for selects, with its section.</summary>
public sealed record CategoryOption(int Id, string Name, int SectionId);

/// <summary>Section choice for selects.</summary>
public sealed record SectionOption(int Id, string Name);

/// <summary>Tag choice for selects, with its section.</summary>
public sealed record TagOption(int Id, string Name, int SectionId);

/// <summary>Editable image row of a product.</summary>
public sealed class ProductMediaForm
{
    public ProductMediaKind Kind { get; set; } = ProductMediaKind.Gallery;
    public string Url { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Editable information row of a product.</summary>
public sealed class ProductFactForm
{
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Editable stock quantity of a product at one location.</summary>
public sealed class LocationStockForm
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>Editable product. Prices are entered in MZN. SectionId only narrows the category and tag choices.</summary>
public sealed class ProductForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public int CategoryId { get; set; }
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string? Badge { get; set; }
    public ProductKind Kind { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public List<int> TagIds { get; set; } = [];
    public List<LocationStockForm> Stocks { get; set; } = [];
    public string? DigitalFileName { get; set; }
    public long? DigitalFileSizeBytes { get; set; }
    public List<ProductMediaForm> Media { get; set; } = [];
    public List<ProductFactForm> Facts { get; set; } = [];
}
