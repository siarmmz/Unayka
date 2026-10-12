namespace Unayka.Core.Catalog;

public enum ProductSort
{
    Relevance,
    PriceAscending,
    PriceDescending,
    Newest,
    BestSellers,
}

/// <summary>Filters, sort and page requested by the catalog. TagSlugs keeps only products that have every listed tag.</summary>
public sealed record ProductSearch(
    string? Query = null,
    string? SectionSlug = null,
    string? CategorySlug = null,
    IReadOnlyList<string>? TagSlugs = null,
    long? MinPriceMinor = null,
    long? MaxPriceMinor = null,
    ProductSort Sort = ProductSort.Relevance,
    int Page = 1);

/// <summary>One page of catalog results.</summary>
public sealed record ProductPage(IReadOnlyList<ProductCardModel> Items, int Page, int PageCount, int TotalCount);

/// <summary>Category with the number of active products.</summary>
public sealed record CategorySummary(string Slug, string Name, string Description, string SectionSlug, string ImageUrl, int ProductCount);

/// <summary>Tag of a section.</summary>
public sealed record TagSummary(string Slug, string Name);

/// <summary>Active section with its active categories and tags in sort order.</summary>
public sealed record SectionSummary(
    string Slug,
    string Name,
    string Description,
    string ImageUrl,
    int ProductCount,
    IReadOnlyList<CategorySummary> Categories,
    IReadOnlyList<TagSummary> Tags);

/// <summary>Image of a product detail page.</summary>
public sealed record ProductImage(ProductMediaKind Kind, string Url, string Alt);

/// <summary>Facts of one group, such as the nutrition table.</summary>
public sealed record ProductFactGroup(string Name, IReadOnlyList<(string Label, string Value)> Rows);

/// <summary>Everything the product page shows. StockQuantity is the total across active locations (unlimited for digital products).</summary>
public sealed record ProductDetail(
    int Id,
    string Slug,
    string Name,
    string ShortDescription,
    string Description,
    long PriceMinor,
    long? CompareAtPriceMinor,
    string? Badge,
    bool IsNew,
    int StockQuantity,
    string CategorySlug,
    string CategoryName,
    string SectionSlug,
    string SectionName,
    ProductKind Kind,
    IReadOnlyList<ProductImage> Images,
    IReadOnlyList<ProductFactGroup> FactGroups,
    IReadOnlyList<TagSummary> Tags);
