using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Unayka.Core.Catalog;

namespace Unayka.Web.Components.Store;

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
        var query = QueryHelpers.ParseQuery(new Uri(uri).Query);
        var sortValue = query.TryGetValue(SortKey, out var rawSort) ? rawSort.ToString() : string.Empty;
        return new ProductSearch(
            query.TryGetValue(QueryKey, out var text) ? text.ToString().Trim() : null,
            sectionSlug,
            categorySlug,
            query.TryGetValue(TagKey, out var tags) ? tags.OfType<string>().Where(tag => tag.Length > 0).Distinct().ToList() : null,
            ReadMinor(query, MinKey),
            ReadMinor(query, MaxKey),
            SortOptions.FirstOrDefault(option => option.Value == sortValue)?.Sort ?? ProductSort.Relevance,
            query.TryGetValue(PageKey, out var page) && int.TryParse(page, CultureInfo.InvariantCulture, out var number) ? Math.Max(1, number) : 1);
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

    private static long? ReadMinor(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key) =>
        query.TryGetValue(key, out var value) && long.TryParse(value, CultureInfo.InvariantCulture, out var units) && units >= 0
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
