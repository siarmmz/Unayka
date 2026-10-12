using Unayka.Core.Catalog;

namespace Unayka.Core.Administration;

/// <summary>Row of the product list.</summary>
public sealed record ProductRow(int Id, string Name, string Slug, string CategoryName, long PriceMinor, long? CompareAtPriceMinor, int StockQuantity, bool IsActive, bool IsFeatured, string? ImageUrl);

/// <summary>Row of the category list.</summary>
public sealed record CategoryRow(int Id, string Name, string Slug, string SectionName, int ProductCount, int SortOrder, string ImageUrl);

/// <summary>Category choice for selects.</summary>
public sealed record CategoryOption(int Id, string Name);

/// <summary>Section choice for selects.</summary>
public sealed record SectionOption(int Id, string Name);

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

/// <summary>Editable product. Prices are entered in MZN.</summary>
public sealed class ProductForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string? Badge { get; set; }
    public ProductKind Kind { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public bool IsActive { get; set; } = true;
    public int StockQuantity { get; set; }
    public List<ProductMediaForm> Media { get; set; } = [];
    public List<ProductFactForm> Facts { get; set; } = [];
}

/// <summary>Editable category.</summary>
public sealed class CategoryForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
