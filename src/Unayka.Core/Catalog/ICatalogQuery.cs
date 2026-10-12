namespace Unayka.Core.Catalog;

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
