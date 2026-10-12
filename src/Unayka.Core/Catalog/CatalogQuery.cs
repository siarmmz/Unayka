// EF Core translates only the parameterless ToLower, which SQL databases run without culture.
#pragma warning disable CA1304, CA1311
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Catalog;

public sealed class CatalogQuery(UnaykaDbContext db) : ICatalogQuery
{
    public const int PageSize = 12;

    private const string LikeEscape = "\\";
    private const int NameMatchScore = 4;
    private const int CategoryMatchScore = 2;
    private const int DescriptionMatchScore = 1;

    public async Task<IReadOnlyList<ProductCardModel>> FeaturedAsync(int count, CancellationToken cancellationToken = default) =>
        await Active()
            .Where(product => product.IsFeatured)
            .OrderByDescending(product => product.SalesCount)
            .Take(count)
            .Select(ProductCardModel.FromProduct)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductCardModel>> BestSellersAsync(int count, CancellationToken cancellationToken = default) =>
        await Active()
            .OrderByDescending(product => product.SalesCount)
            .ThenBy(product => product.Name)
            .Take(count)
            .Select(ProductCardModel.FromProduct)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductCardModel>> NewArrivalsAsync(int count, CancellationToken cancellationToken = default) =>
        await Active()
            .OrderByDescending(product => product.CreatedAt)
            .ThenBy(product => product.Name)
            .Take(count)
            .Select(ProductCardModel.FromProduct)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CategorySummary>> CategoriesAsync(CategoryKind? kind = null, CancellationToken cancellationToken = default) =>
        await db.Categories
            .AsNoTracking()
            .Where(category => kind == null || category.Kind == kind)
            .OrderBy(category => category.SortOrder)
            .Select(category => new CategorySummary(
                category.Slug,
                category.Name,
                category.Description,
                category.Kind,
                category.ImageUrl,
                category.Products.Count(product => product.IsActive)))
            .ToListAsync(cancellationToken);

    public async Task<CategorySummary?> FindCategoryAsync(string slug, CancellationToken cancellationToken = default) =>
        await db.Categories
            .AsNoTracking()
            .Where(category => category.Slug == slug)
            .Select(category => new CategorySummary(
                category.Slug,
                category.Name,
                category.Description,
                category.Kind,
                category.ImageUrl,
                category.Products.Count(product => product.IsActive)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyFilters(Active(), search);
        var total = await filtered.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(search.Page, 1, pageCount);
        var items = await ApplySort(filtered, search)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(ProductCardModel.FromProduct)
            .ToListAsync(cancellationToken);
        return new ProductPage(items, page, pageCount, total);
    }

    public async Task<ProductDetail?> FindProductAsync(string slug, CancellationToken cancellationToken = default)
    {
        var product = await Active()
            .Include(candidate => candidate.Category)
            .Include(candidate => candidate.Media)
            .Include(candidate => candidate.Facts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);
        return product is null ? null : ToDetail(product);
    }

    public async Task<IReadOnlyList<ProductCardModel>> RelatedAsync(string slug, int count, CancellationToken cancellationToken = default)
    {
        var categoryId = await db.Products
            .Where(product => product.Slug == slug)
            .Select(product => product.CategoryId)
            .FirstOrDefaultAsync(cancellationToken);
        return await Active()
            .Where(product => product.CategoryId == categoryId && product.Slug != slug)
            .OrderByDescending(product => product.SalesCount)
            .Take(count)
            .Select(ProductCardModel.FromProduct)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Product> Active() =>
        db.Products.AsNoTracking().Where(product => product.IsActive);

    private static IQueryable<Product> ApplyFilters(IQueryable<Product> products, ProductSearch search)
    {
        if (search.Kind is { } kind)
        {
            products = products.Where(product => product.Category!.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(search.CategorySlug))
        {
            products = products.Where(product => product.Category!.Slug == search.CategorySlug);
        }

        if (search.MinPriceMinor is { } min)
        {
            products = products.Where(product => product.PriceMinor >= min);
        }

        if (search.MaxPriceMinor is { } max)
        {
            products = products.Where(product => product.PriceMinor <= max);
        }

        var term = NormalizeQuery(search.Query);
        return term is null ? products : products.Where(MatchesTerm(ToPattern(term)));
    }

    private static Expression<Func<Product, bool>> MatchesTerm(string pattern) =>
        product => EF.Functions.Like(product.Name.ToLower(), pattern, LikeEscape)
                   || EF.Functions.Like(product.ShortDescription.ToLower(), pattern, LikeEscape)
                   || EF.Functions.Like(product.Description.ToLower(), pattern, LikeEscape)
                   || EF.Functions.Like(product.Category!.Name.ToLower(), pattern, LikeEscape);

    private static IQueryable<Product> ApplySort(IQueryable<Product> products, ProductSearch search) =>
        search.Sort switch
        {
            ProductSort.PriceAscending => products.OrderBy(product => product.PriceMinor).ThenBy(product => product.Name),
            ProductSort.PriceDescending => products.OrderByDescending(product => product.PriceMinor).ThenBy(product => product.Name),
            ProductSort.Newest => products.OrderByDescending(product => product.CreatedAt).ThenBy(product => product.Name),
            ProductSort.BestSellers => products.OrderByDescending(product => product.SalesCount).ThenBy(product => product.Name),
            _ => SortByRelevance(products, NormalizeQuery(search.Query)),
        };

    private static IQueryable<Product> SortByRelevance(IQueryable<Product> products, string? term)
    {
        if (term is null)
        {
            return products.OrderByDescending(product => product.IsFeatured).ThenByDescending(product => product.SalesCount).ThenBy(product => product.Name);
        }

        var pattern = ToPattern(term);
        return products
            .OrderByDescending(product =>
                (EF.Functions.Like(product.Name.ToLower(), pattern, LikeEscape) ? NameMatchScore : 0)
                + (EF.Functions.Like(product.Category!.Name.ToLower(), pattern, LikeEscape) ? CategoryMatchScore : 0)
                + (EF.Functions.Like(product.Description.ToLower(), pattern, LikeEscape) ? DescriptionMatchScore : 0))
            .ThenByDescending(product => product.SalesCount)
            .ThenBy(product => product.Name);
    }

    private static string ToPattern(string term) =>
        $"%{term.Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal).Replace("%", LikeEscape + "%", StringComparison.Ordinal).Replace("_", LikeEscape + "_", StringComparison.Ordinal)}%";

    private static string? NormalizeQuery(string? query) =>
        string.IsNullOrWhiteSpace(query) ? null : query.Trim().ToLowerInvariant();

    private static ProductDetail ToDetail(Product product)
    {
        var images = product.Media
            .OrderBy(media => media.SortOrder)
            .Select(media => new ProductImage(media.Kind, media.Url, media.Alt))
            .ToList();
        var groups = product.Facts
            .OrderBy(fact => fact.SortOrder)
            .GroupBy(fact => fact.Group)
            .Select(group => new ProductFactGroup(group.Key, group.Select(fact => (fact.Label, fact.Value)).ToList()))
            .ToList();
        return new ProductDetail(
            product.Id,
            product.Slug,
            product.Name,
            product.ShortDescription,
            product.Description,
            product.PriceMinor,
            product.CompareAtPriceMinor,
            product.Badge,
            product.StockQuantity,
            product.Category!.Slug,
            product.Category.Name,
            product.Category.Kind,
            images,
            groups);
    }
}
