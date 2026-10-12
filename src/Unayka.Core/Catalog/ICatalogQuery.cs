namespace Unayka.Core.Catalog;

public interface ICatalogQuery
{
    /// <summary>Featured active products.</summary>
    Task<IReadOnlyList<ProductCardModel>> FeaturedAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Active products with the most sales.</summary>
    Task<IReadOnlyList<ProductCardModel>> BestSellersAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Most recently added active products.</summary>
    Task<IReadOnlyList<ProductCardModel>> NewArrivalsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Categories with product counts, optionally of one kind.</summary>
    Task<IReadOnlyList<CategorySummary>> CategoriesAsync(CategoryKind? kind = null, CancellationToken cancellationToken = default);

    /// <summary>One category by slug, or null.</summary>
    Task<CategorySummary?> FindCategoryAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Filtered, sorted and paged active products.</summary>
    Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken = default);

    /// <summary>Active product by slug with media and facts, or null.</summary>
    Task<ProductDetail?> FindProductAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Other active products of the same category.</summary>
    Task<IReadOnlyList<ProductCardModel>> RelatedAsync(string slug, int count, CancellationToken cancellationToken = default);
}
