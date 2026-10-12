using System.Linq.Expressions;
using Unayka.Core.Locations;

namespace Unayka.Core.Catalog;

public enum ProductKind
{
    Physical,
    Digital,
}

public enum CatalogStatus
{
    Active,
    Inactive,
    Archived,
}

public enum ProductMediaKind
{
    Prepared,
    Package,
    Elements,
    Gallery,
    Jar,
    Applied,
    Cover,
}

/// <summary>Top level of the catalog, such as teas, cosmetics or books.</summary>
public sealed class Section
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool RequiresPreparation { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public List<Category> Categories { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
}

/// <summary>Group of products inside a section.</summary>
public sealed class Category
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public Section? Section { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public List<Product> Products { get; set; } = [];
}

/// <summary>Free label scoped to a section, used to filter products.</summary>
public sealed class Tag
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public Section? Section { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<Product> Products { get; set; } = [];
}

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

/// <summary>Image of a product. The first by SortOrder is the card image.</summary>
public sealed class ProductMedia
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public ProductMediaKind Kind { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
    public string? Credit { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>One row of a product information table, such as nutrition or composition.</summary>
public sealed class ProductFact
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Downloadable file of a digital product, kept in private storage.</summary>
public sealed class DigitalAsset
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

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
/// <summary>Product image; Credit names the author and license when the photo requires attribution.</summary>
public sealed record ProductImage(ProductMediaKind Kind, string Url, string Alt, string? Credit);

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
    bool IsFeatured,
    int StockQuantity,
    string CategorySlug,
    string CategoryName,
    string SectionSlug,
    string SectionName,
    ProductKind Kind,
    IReadOnlyList<ProductImage> Images,
    IReadOnlyList<ProductFactGroup> FactGroups,
    IReadOnlyList<TagSummary> Tags);

/// <summary>Data shown on a product card in listings. ImageUrls are ordered for the card cross fade.</summary>
public sealed record ProductCardModel(
    int Id,
    string Slug,
    string Name,
    string CategoryName,
    long PriceMinor,
    long? CompareAtPriceMinor,
    string ImageUrl,
    IReadOnlyList<string> ImageUrls,
    string? Badge,
    bool IsNew,
    bool IsFeatured,
    ProductKind Kind,
    bool InStock)
{
    public static readonly Expression<Func<Product, ProductCardModel>> FromProduct = product => new ProductCardModel(
        product.Id,
        product.Slug,
        product.Name,
        product.Category!.Name,
        product.PriceMinor,
        product.CompareAtPriceMinor,
        product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault() ?? string.Empty,
        product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).ToList(),
        product.Badge,
        product.IsNew,
        product.IsFeatured,
        product.Kind,
        product.Kind == ProductKind.Digital
        || product.Stocks.Any(stock => stock.Quantity > 0 && stock.Location!.Status == CatalogStatus.Active));
}

public enum StockLevel
{
    InStock,
    LowStock,
    OutOfStock,
}

public static class StockLevels
{
    public const int LowStockThreshold = 5;

    public static StockLevel For(int stockQuantity) => stockQuantity switch
    {
        <= 0 => StockLevel.OutOfStock,
        <= LowStockThreshold => StockLevel.LowStock,
        _ => StockLevel.InStock,
    };
}

public static class ProductStock
{
    public const int Unlimited = int.MaxValue;

    /// <summary>Quantity available across active locations, always unlimited for digital products. Needs Stocks with Location loaded.</summary>
    public static int Total(Product product) =>
        product.Kind == ProductKind.Digital ? Unlimited : product.Stocks.Where(IsAtActiveLocation).Sum(stock => stock.Quantity);

    private static bool IsAtActiveLocation(LocationStock stock) => stock.Location?.Status == CatalogStatus.Active;
}

public static class ProductVisibility
{
    /// <summary>Product that customers may see and buy: active, in an active category of an active section.</summary>
    public static readonly Expression<Func<Product, bool>> IsPublic = product =>
        product.Status == CatalogStatus.Active
        && product.Category!.Status == CatalogStatus.Active
        && product.Category.Section!.Status == CatalogStatus.Active;
}

public static class StoreUrls
{
    public const string Shop = "/loja";
    public const string AllProducts = "/loja/todos";
    public const string Cart = "/carrinho";
    public const string Checkout = "/checkout";
    public const string Account = "/conta";
    public const string SignIn = "/conta/entrar";
    public const string SignOut = "/conta/sair";
    public const string Admin = "/admin";

    public static string Product(string slug) => $"/produto/{slug}";

    public static string Section(string slug) => $"{Shop}/{slug}";

    public static string Category(string slug) => $"/loja/categoria/{slug}";

    public static string Search(string term) => $"{AllProducts}?q={Uri.EscapeDataString(term)}";
}

public interface ICatalogQuery
{
    /// <summary>Featured public products.</summary>
    Task<IReadOnlyList<ProductCardModel>> FeaturedAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Public products with the most sales.</summary>
    Task<IReadOnlyList<ProductCardModel>> BestSellersAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Most recently added public products.</summary>
    Task<IReadOnlyList<ProductCardModel>> NewArrivalsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Active sections with their active categories and tags, in sort order.</summary>
    Task<IReadOnlyList<SectionSummary>> SectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>One active section by slug, or null.</summary>
    Task<SectionSummary?> FindSectionAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Featured and best selling public products of one section.</summary>
    Task<IReadOnlyList<ProductCardModel>> BySectionAsync(string sectionSlug, int count, CancellationToken cancellationToken = default);

    /// <summary>Active categories with product counts, optionally of one section.</summary>
    Task<IReadOnlyList<CategorySummary>> CategoriesAsync(string? sectionSlug = null, CancellationToken cancellationToken = default);

    /// <summary>One active category by slug, or null.</summary>
    Task<CategorySummary?> FindCategoryAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Filtered, sorted and paged public products.</summary>
    Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken = default);

    /// <summary>Public product by slug with media, facts and tags, or null.</summary>
    Task<ProductDetail?> FindProductAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Other public products of the same category.</summary>
    Task<IReadOnlyList<ProductCardModel>> RelatedAsync(string slug, int count, CancellationToken cancellationToken = default);
}

public interface IDigitalFileStore
{
    /// <summary>Writes a private digital file under the given key, replacing any previous file.</summary>
    Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens a private digital file by storage key, or returns null when it does not exist.</summary>
    Stream? Open(string storageKey);

    /// <summary>Removes a private digital file when it exists.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
