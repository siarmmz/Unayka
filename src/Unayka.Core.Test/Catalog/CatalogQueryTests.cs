using Unayka.Core.Catalog;
using Unayka.Core.Data.Seed;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Catalog;

public sealed class CatalogQueryTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly CatalogQuery _query;

    public CatalogQueryTests()
    {
        DatabaseSeeder.SeedAsync(_database.Context, new SeedOptions("unayka2026", "admin@unayka.co.mz", false), TimeProvider.System).GetAwaiter().GetResult();
        _query = new CatalogQuery(_database.Context);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Search_pages_results_by_twelve()
    {
        var first = await _query.SearchAsync(new ProductSearch());
        var second = await _query.SearchAsync(new ProductSearch(Page: 2));

        Assert.Equal(28, first.TotalCount);
        Assert.Equal(3, first.PageCount);
        Assert.Equal(CatalogQuery.PageSize, first.Items.Count);
        Assert.Empty(first.Items.Select(item => item.Slug).Intersect(second.Items.Select(item => item.Slug)));
    }

    [Fact]
    public async Task Search_clamps_page_beyond_the_last()
    {
        var result = await _query.SearchAsync(new ProductSearch(Page: 99));

        Assert.Equal(result.PageCount, result.Page);
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task Search_filters_by_kind()
    {
        var teas = await _query.SearchAsync(new ProductSearch(Kind: CategoryKind.Tea, Sort: ProductSort.BestSellers));
        var categories = (await _query.CategoriesAsync(CategoryKind.Tea)).Select(category => category.Name).ToHashSet();

        Assert.NotEmpty(teas.Items);
        Assert.All(teas.Items, item => Assert.Contains(item.CategoryName, categories));
    }

    [Fact]
    public async Task Search_filters_by_category_and_price_range()
    {
        var category = (await _query.CategoriesAsync(CategoryKind.Cosmetic))[0];
        var ranged = await _query.SearchAsync(new ProductSearch(CategorySlug: category.Slug, MinPriceMinor: 0, MaxPriceMinor: 50000));

        Assert.All(ranged.Items, item =>
        {
            Assert.Equal(category.Name, item.CategoryName);
            Assert.InRange(item.PriceMinor, 0, 50000);
        });
    }

    [Fact]
    public async Task Search_matches_text_case_insensitively_and_ranks_name_first()
    {
        var result = await _query.SearchAsync(new ProductSearch(Query: "CAPPUCCINO"));

        Assert.NotEmpty(result.Items);
        Assert.Equal("cappuccino-cremoso", result.Items[0].Slug);
    }

    [Fact]
    public async Task Search_without_matches_returns_one_empty_page()
    {
        var result = await _query.SearchAsync(new ProductSearch(Query: "zzzzzz"));

        Assert.Empty(result.Items);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Search_sorts_by_price_in_both_directions()
    {
        var ascending = (await _query.SearchAsync(new ProductSearch(Sort: ProductSort.PriceAscending))).Items.Select(item => item.PriceMinor).ToList();
        var descending = (await _query.SearchAsync(new ProductSearch(Sort: ProductSort.PriceDescending))).Items.Select(item => item.PriceMinor).ToList();

        Assert.Equal(ascending.Order(), ascending);
        Assert.Equal(descending.OrderDescending(), descending);
    }

    [Fact]
    public async Task Search_excludes_inactive_products()
    {
        var target = _database.Context.Products.OrderBy(product => product.Id).First();
        target.IsActive = false;
        await _database.Context.SaveChangesAsync();

        var result = await _query.SearchAsync(new ProductSearch());

        Assert.Equal(27, result.TotalCount);
        Assert.Null(await _query.FindProductAsync(target.Slug));
    }

    [Fact]
    public async Task Best_sellers_are_ordered_by_sales()
    {
        var best = await _query.BestSellersAsync(5);

        Assert.Equal(5, best.Count);
        var sales = _database.Context.Products.ToDictionary(product => product.Slug, product => product.SalesCount);
        Assert.Equal(best.Select(item => sales[item.Slug]).OrderDescending(), best.Select(item => sales[item.Slug]));
    }

    [Fact]
    public async Task Featured_returns_only_featured_products()
    {
        var featured = await _query.FeaturedAsync(8);
        var flags = _database.Context.Products.ToDictionary(product => product.Slug, product => product.IsFeatured);

        Assert.NotEmpty(featured);
        Assert.All(featured, item => Assert.True(flags[item.Slug]));
    }

    [Fact]
    public async Task Categories_report_product_counts()
    {
        var categories = await _query.CategoriesAsync();

        Assert.Equal(7, categories.Count);
        Assert.Equal(28, categories.Sum(category => category.ProductCount));
        Assert.Null(await _query.FindCategoryAsync("does-not-exist"));
    }

    [Fact]
    public async Task Detail_orders_media_and_groups_facts()
    {
        var detail = await _query.FindProductAsync("cappuccino-cremoso");

        Assert.NotNull(detail);
        Assert.Equal(ProductMediaKind.Prepared, detail.Images[0].Kind);
        Assert.NotEmpty(detail.FactGroups);
        Assert.All(detail.FactGroups, group => Assert.NotEmpty(group.Rows));
    }

    [Fact]
    public async Task Related_products_share_the_category_and_exclude_the_product()
    {
        var detail = await _query.FindProductAsync("cappuccino-cremoso");
        var related = await _query.RelatedAsync("cappuccino-cremoso", 4);

        Assert.NotEmpty(related);
        Assert.All(related, item =>
        {
            Assert.Equal(detail!.CategoryName, item.CategoryName);
            Assert.NotEqual("cappuccino-cremoso", item.Slug);
        });
    }
}
