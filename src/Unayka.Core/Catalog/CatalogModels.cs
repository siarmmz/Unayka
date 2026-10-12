namespace Unayka.Core.Catalog;

public enum ProductSort
{
    Relevance,
    PriceAscending,
    PriceDescending,
    Newest,
    BestSellers,
}

/// <summary>Filters, sort and page requested by the catalog.</summary>
public sealed record ProductSearch(
    string? Query = null,
    CategoryKind? Kind = null,
    string? CategorySlug = null,
    long? MinPriceMinor = null,
    long? MaxPriceMinor = null,
    ProductSort Sort = ProductSort.Relevance,
    int Page = 1);

/// <summary>One page of catalog results.</summary>
public sealed record ProductPage(IReadOnlyList<ProductCardModel> Items, int Page, int PageCount, int TotalCount);

/// <summary>Category with the number of active products.</summary>
public sealed record CategorySummary(string Slug, string Name, string Description, CategoryKind Kind, string ImageUrl, int ProductCount);

/// <summary>Image of a product detail page.</summary>
public sealed record ProductImage(ProductMediaKind Kind, string Url, string Alt);

/// <summary>Facts of one group, such as the nutrition table.</summary>
public sealed record ProductFactGroup(string Name, IReadOnlyList<(string Label, string Value)> Rows);

/// <summary>Everything the product page shows.</summary>
public sealed record ProductDetail(
    int Id,
    string Slug,
    string Name,
    string ShortDescription,
    string Description,
    long PriceMinor,
    long? CompareAtPriceMinor,
    string? Badge,
    int StockQuantity,
    string CategorySlug,
    string CategoryName,
    CategoryKind Kind,
    IReadOnlyList<ProductImage> Images,
    IReadOnlyList<ProductFactGroup> FactGroups);
