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

    public async Task<IReadOnlyList<SectionSummary>> SectionsAsync(CancellationToken cancellationToken = default)
    {
        var sections = await db.Sections
            .AsNoTracking()
            .Include(section => section.Categories.Where(category => category.Status == CatalogStatus.Active).OrderBy(category => category.SortOrder))
            .Include(section => section.Tags.OrderBy(tag => tag.SortOrder))
            .Where(section => section.Status == CatalogStatus.Active)
            .OrderBy(section => section.SortOrder)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var counts = await CountByCategoryAsync(cancellationToken);
        return sections.Select(section => ToSummary(section, counts)).ToList();
    }

    public async Task<SectionSummary?> FindSectionAsync(string slug, CancellationToken cancellationToken = default) =>
        (await SectionsAsync(cancellationToken)).FirstOrDefault(section => section.Slug == slug);

    public async Task<IReadOnlyList<ProductCardModel>> BySectionAsync(string sectionSlug, int count, CancellationToken cancellationToken = default) =>
        await Active()
            .Where(product => product.Category!.Section!.Slug == sectionSlug)
            .OrderByDescending(product => product.IsFeatured)
            .ThenByDescending(product => product.SalesCount)
            .ThenBy(product => product.Name)
            .Take(count)
            .Select(ProductCardModel.FromProduct)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CategorySummary>> CategoriesAsync(string? sectionSlug = null, CancellationToken cancellationToken = default) =>
        await ActiveCategories()
            .Where(category => sectionSlug == null || category.Section!.Slug == sectionSlug)
            .OrderBy(category => category.Section!.SortOrder)
            .ThenBy(category => category.SortOrder)
            .Select(ToCategorySummary)
            .ToListAsync(cancellationToken);

    public async Task<CategorySummary?> FindCategoryAsync(string slug, CancellationToken cancellationToken = default) =>
        await ActiveCategories()
            .Where(category => category.Slug == slug)
            .Select(ToCategorySummary)
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
            .Include(candidate => candidate.Category!.Section)
            .Include(candidate => candidate.Media)
            .Include(candidate => candidate.Facts)
            .Include(candidate => candidate.Tags)
            .Include(candidate => candidate.Stocks).ThenInclude(stock => stock.Location)
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
        db.Products.AsNoTracking().Where(ProductVisibility.IsPublic);

    private IQueryable<Category> ActiveCategories() =>
        db.Categories.AsNoTracking().Where(category =>
            category.Status == CatalogStatus.Active && category.Section!.Status == CatalogStatus.Active);

    private static readonly Expression<Func<Category, CategorySummary>> ToCategorySummary = category => new CategorySummary(
        category.Slug,
        category.Name,
        category.Description,
        category.Section!.Slug,
        category.ImageUrl,
        category.Products.Count(product => product.Status == CatalogStatus.Active));

    private async Task<Dictionary<int, int>> CountByCategoryAsync(CancellationToken cancellationToken) =>
        await Active()
            .GroupBy(product => product.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.CategoryId, row => row.Count, cancellationToken);

    private static SectionSummary ToSummary(Section section, Dictionary<int, int> counts)
    {
        var categories = section.Categories
            .Select(category => new CategorySummary(
                category.Slug, category.Name, category.Description, section.Slug, category.ImageUrl, counts.GetValueOrDefault(category.Id)))
            .ToList();
        return new SectionSummary(
            section.Slug,
            section.Name,
            section.Description,
            section.ImageUrl,
            categories.Sum(category => category.ProductCount),
            categories,
            section.Tags.Select(tag => new TagSummary(tag.Slug, tag.Name)).ToList());
    }

    private static IQueryable<Product> ApplyFilters(IQueryable<Product> products, ProductSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.SectionSlug))
        {
            products = products.Where(product => product.Category!.Section!.Slug == search.SectionSlug);
        }

        foreach (var tagSlug in search.TagSlugs ?? [])
        {
            var slug = tagSlug;
            products = products.Where(product => product.Tags.Any(tag => tag.Slug == slug));
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
            product.IsNew,
            ProductStock.Total(product),
            product.Category!.Slug,
            product.Category.Name,
            product.Category.Section!.Slug,
            product.Category.Section.Name,
            product.Kind,
            images,
            groups,
            product.Tags.OrderBy(tag => tag.SortOrder).Select(tag => new TagSummary(tag.Slug, tag.Name)).ToList());
    }
}
