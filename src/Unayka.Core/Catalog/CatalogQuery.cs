using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unayka.Core.Configuration;
using Unayka.Core.Data;

namespace Unayka.Core.Catalog;

// EF Core translates only the parameterless ToLower, which SQL databases run without culture.
#pragma warning disable CA1304, CA1311

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
        var categoryId = await Active()
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
            .Select(media => new ProductImage(media.Kind, media.Url, media.Alt, media.Credit))
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
            product.IsFeatured,
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

public sealed class LocalDigitalFileStore : IDigitalFileStore
{
    private const string DigitalFolder = "digital";

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var path = PathFor(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, cancellationToken);
    }

    public Stream? Open(string storageKey)
    {
        if (string.IsNullOrEmpty(storageKey) || Path.GetFileName(storageKey) != storageKey)
        {
            return null;
        }

        var path = PathFor(storageKey);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PathFor(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static string PathFor(string storageKey) =>
        Path.Combine(AppEnvConfig.LocalUploadsPath, DigitalFolder, Path.GetFileName(storageKey));
}

public static class CatalogServiceExtensions
{
    public static IServiceCollection AddUnaykaCatalog(this IServiceCollection services)
    {
        services.AddScoped<ICatalogQuery, CatalogQuery>();
        services.AddSingleton<IDigitalFileStore, LocalDigitalFileStore>();
        return services;
    }
}

/// <summary>Sort choice offered on catalog pages.</summary>
public sealed record SortOption(string Value, string Label, ProductSort Sort);

public static class CatalogLinks
{
    public const string QueryKey = "q";
    public const string SortKey = "ordem";
    public const string MinKey = "de";
    public const string MaxKey = "ate";
    public const string PageKey = "page";
    public const string TagKey = "etiqueta";

    private const long MinorUnitsPerUnit = 100;

    public static readonly IReadOnlyList<SortOption> SortOptions =
    [
        new("relevancia", "Sugestões", ProductSort.Relevance),
        new("preco-asc", "Preço, do mais baixo", ProductSort.PriceAscending),
        new("preco-desc", "Preço, do mais alto", ProductSort.PriceDescending),
        new("novidades", "Novidades", ProductSort.Newest),
        new("vendidos", "Mais vendidos", ProductSort.BestSellers),
    ];

    public static ProductSearch Parse(string uri, string? sectionSlug, string? categorySlug)
    {
        var query = ParseQuery(new Uri(uri).Query);
        var sortValue = query.TryGetValue(SortKey, out var rawSort) ? rawSort[0] : string.Empty;
        return new ProductSearch(
            query.TryGetValue(QueryKey, out var text) ? text[0].Trim() : null,
            sectionSlug,
            categorySlug,
            query.TryGetValue(TagKey, out var tags) ? tags.Where(tag => tag.Length > 0).Distinct().ToList() : null,
            ReadMinor(query, MinKey),
            ReadMinor(query, MaxKey),
            SortOptions.FirstOrDefault(option => option.Value == sortValue)?.Sort ?? ProductSort.Relevance,
            query.TryGetValue(PageKey, out var page) && int.TryParse(page[0], CultureInfo.InvariantCulture, out var number) ? Math.Max(1, number) : 1);
    }

    public static string Build(string basePath, ProductSearch search, bool includePage = false)
    {
        var parts = new List<string>();
        AddIf(parts, QueryKey, search.Query);
        foreach (var tag in search.TagSlugs ?? [])
        {
            AddIf(parts, TagKey, tag);
        }

        AddIf(parts, MinKey, search.MinPriceMinor is { } min ? (min / MinorUnitsPerUnit).ToString(CultureInfo.InvariantCulture) : null);
        AddIf(parts, MaxKey, search.MaxPriceMinor is { } max ? (max / MinorUnitsPerUnit).ToString(CultureInfo.InvariantCulture) : null);
        AddIf(parts, SortKey, search.Sort == ProductSort.Relevance ? null : SortOptions.First(option => option.Sort == search.Sort).Value);
        AddIf(parts, PageKey, includePage && search.Page > 1 ? search.Page.ToString(CultureInfo.InvariantCulture) : null);
        return parts.Count == 0 ? basePath : $"{basePath}?{string.Join('&', parts)}";
    }

    public static ProductSearch ToggleTag(ProductSearch search, string tagSlug)
    {
        var tags = (search.TagSlugs ?? []).ToList();
        if (!tags.Remove(tagSlug))
        {
            tags.Add(tagSlug);
        }

        return search with { TagSlugs = tags, Page = 1 };
    }

    public static IReadOnlyList<(string Name, string Value)> PriceFields(ProductSearch search)
    {
        var fields = new List<(string Name, string Value)>();
        if (search.MinPriceMinor is { } min)
        {
            fields.Add((MinKey, (min / MinorUnitsPerUnit).ToString(CultureInfo.InvariantCulture)));
        }

        if (search.MaxPriceMinor is { } max)
        {
            fields.Add((MaxKey, (max / MinorUnitsPerUnit).ToString(CultureInfo.InvariantCulture)));
        }

        return fields;
    }

    private static Dictionary<string, List<string>> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(parts => (Key: Decode(parts[0]), Value: parts.Length > 1 ? Decode(parts[1]) : string.Empty))
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Value).ToList(), StringComparer.OrdinalIgnoreCase);

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static long? ReadMinor(Dictionary<string, List<string>> query, string key) =>
        query.TryGetValue(key, out var value) && long.TryParse(value[0], CultureInfo.InvariantCulture, out var units) && units >= 0
            ? units * MinorUnitsPerUnit
            : null;

    private static void AddIf(List<string> parts, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{key}={Uri.EscapeDataString(value)}");
        }
    }
}
