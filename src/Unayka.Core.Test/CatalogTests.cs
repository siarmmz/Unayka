using Microsoft.EntityFrameworkCore;
using Unayka.Core.Catalog;
using Unayka.Core.Locations;

namespace Unayka.Core.Test;

public sealed class CatalogQueryTests : IDisposable
{
    private readonly TestDatabase _database = new(withStoreData: true);
    private readonly CatalogQuery _query;

    public CatalogQueryTests()
    {
        _query = new CatalogQuery(_database.Context);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Search_pages_results_by_twelve()
    {
        var first = await _query.SearchAsync(new ProductSearch());
        var second = await _query.SearchAsync(new ProductSearch(Page: 2));

        var total = await _database.Context.Products.CountAsync();
        Assert.Equal(total, first.TotalCount);
        Assert.Equal((total + CatalogQuery.PageSize - 1) / CatalogQuery.PageSize, first.PageCount);
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
    public async Task Search_filters_by_section()
    {
        var teas = await _query.SearchAsync(new ProductSearch(SectionSlug: "chas", Sort: ProductSort.BestSellers));
        var categories = (await _query.CategoriesAsync("chas")).Select(category => category.Name).ToHashSet();

        Assert.NotEmpty(teas.Items);
        Assert.All(teas.Items, item => Assert.Contains(item.CategoryName, categories));
    }

    [Fact]
    public async Task Search_filters_by_category_and_price_range()
    {
        var category = (await _query.CategoriesAsync("cosmeticos"))[0];
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
        Assert.Equal("cappuccino", result.Items[0].Slug);
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
    public async Task Search_excludes_inactive_and_archived_products()
    {
        var total = await _database.Context.Products.CountAsync();
        var targets = _database.Context.Products.OrderBy(product => product.Id).Take(2).ToList();
        targets[0].Status = CatalogStatus.Inactive;
        targets[1].Status = CatalogStatus.Archived;
        await _database.Context.SaveChangesAsync();

        var result = await _query.SearchAsync(new ProductSearch());

        Assert.Equal(total - 2, result.TotalCount);
        Assert.Null(await _query.FindProductAsync(targets[0].Slug));
        Assert.Null(await _query.FindProductAsync(targets[1].Slug));
        Assert.DoesNotContain(await _query.NewArrivalsAsync(100), item => item.Slug == targets[0].Slug || item.Slug == targets[1].Slug);
    }

    [Fact]
    public async Task Products_of_inactive_sections_and_categories_are_hidden()
    {
        var cosmetics = await _database.Context.Sections.SingleAsync(section => section.Slug == "cosmeticos");
        cosmetics.Status = CatalogStatus.Inactive;
        var hotCoffee = await _database.Context.Categories.SingleAsync(category => category.Slug == "cafe");
        hotCoffee.Status = CatalogStatus.Archived;
        await _database.Context.SaveChangesAsync();

        var result = await _query.SearchAsync(new ProductSearch());
        var sections = await _query.SectionsAsync();

        Assert.DoesNotContain(result.Items, item => item.CategoryName == hotCoffee.Name);
        Assert.DoesNotContain(sections, section => section.Slug == "cosmeticos");
        Assert.DoesNotContain(sections.SelectMany(section => section.Categories), category => category.Slug == hotCoffee.Slug);
        Assert.Null(await _query.FindCategoryAsync(hotCoffee.Slug));
        Assert.Empty((await _query.SearchAsync(new ProductSearch(SectionSlug: "cosmeticos"))).Items);
        Assert.Null(await _query.FindProductAsync("manteiga-de-karite-200g"));
    }

    [Fact]
    public async Task Sections_list_active_categories_and_tags_in_sort_order()
    {
        var sections = await _query.SectionsAsync();

        Assert.Equal(["chas", "cosmeticos", "livros"], sections.Select(section => section.Slug));
        Assert.All(sections, section => Assert.NotEmpty(section.Categories));
        Assert.Contains(sections[0].Tags, tag => tag.Slug == "calmante");
        Assert.Equal(await _database.Context.Products.CountAsync(), sections.Sum(section => section.ProductCount));
    }

    [Fact]
    public async Task Search_filters_by_tag_inside_a_section()
    {
        var result = await _query.SearchAsync(new ProductSearch(SectionSlug: "chas", TagSlugs: ["sem-cafeina"]));
        var both = await _query.SearchAsync(new ProductSearch(SectionSlug: "chas", TagSlugs: ["sem-cafeina", "calmante"]));

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(["cha-de-camomila", "cha-de-capim-principe"], both.Items.Select(item => item.Slug).Order());
    }

    [Fact]
    public async Task Stock_level_sums_every_active_location()
    {
        var product = await _database.Context.Products.SingleAsync(candidate => candidate.Slug == "espresso");
        var total = await _database.Context.LocationStocks.Where(stock => stock.ProductId == product.Id).SumAsync(stock => stock.Quantity);

        var detail = await _query.FindProductAsync(product.Slug);

        Assert.Equal(500, total);
        Assert.Equal(total, detail!.StockQuantity);
    }

    [Fact]
    public async Task Stock_ignores_inactive_locations_and_marks_out_of_stock()
    {
        var product = await _database.Context.Products.Include(candidate => candidate.Stocks).ThenInclude(stock => stock.Location).SingleAsync(candidate => candidate.Slug == "cappuccino");
        foreach (var stock in product.Stocks.Where(stock => stock.LocationId != product.Stocks[0].LocationId))
        {
            stock.Quantity = 0;
        }

        product.Stocks[0].Location!.Status = CatalogStatus.Inactive;
        await _database.Context.SaveChangesAsync();

        var detail = await _query.FindProductAsync(product.Slug);
        var card = (await _query.SearchAsync(new ProductSearch(Query: "cappuccino"))).Items.Single();

        Assert.Equal(0, detail!.StockQuantity);
        Assert.False(card.InStock);
    }

    [Fact]
    public async Task Digital_products_are_always_available()
    {
        var detail = await _query.FindProductAsync("e-book-receitas-com-cha");
        var card = (await _query.SearchAsync(new ProductSearch(SectionSlug: "livros"))).Items.Single(item => item.Slug == "e-book-receitas-com-cha");

        Assert.Equal(ProductKind.Digital, detail!.Kind);
        Assert.Equal(ProductStock.Unlimited, detail.StockQuantity);
        Assert.True(card.InStock);
        Assert.Equal(ProductKind.Digital, card.Kind);
        Assert.True(card.IsNew);
        Assert.NotEmpty(card.ImageUrls);
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

        Assert.Equal(await _database.Context.Categories.CountAsync(), categories.Count);
        Assert.Equal(await _database.Context.Products.CountAsync(), categories.Sum(category => category.ProductCount));
        Assert.Null(await _query.FindCategoryAsync("does-not-exist"));
    }

    [Fact]
    public async Task Detail_orders_media_and_groups_facts()
    {
        var detail = await _query.FindProductAsync("cappuccino");

        Assert.NotNull(detail);
        Assert.Equal(ProductMediaKind.Prepared, detail.Images[0].Kind);
        Assert.NotEmpty(detail.FactGroups);
        Assert.All(detail.FactGroups, group => Assert.NotEmpty(group.Rows));
    }

    [Fact]
    public async Task Related_products_share_the_category_and_exclude_the_product()
    {
        var detail = await _query.FindProductAsync("cappuccino");
        var related = await _query.RelatedAsync("cappuccino", 4);

        Assert.NotEmpty(related);
        Assert.All(related, item =>
        {
            Assert.Equal(detail!.CategoryName, item.CategoryName);
            Assert.NotEqual("cappuccino", item.Slug);
        });
    }
}

public sealed class PublicVisibilityTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Theory]
    [InlineData(CatalogStatus.Active, true)]
    [InlineData(CatalogStatus.Inactive, false)]
    [InlineData(CatalogStatus.Archived, false)]
    public async Task Product_detail_is_found_by_slug_only_when_active(CatalogStatus status, bool expectedFound)
    {
        await SeedAsync(productStatus: status);

        var detail = await new CatalogQuery(_database.Context).FindProductAsync("cha");

        Assert.Equal(expectedFound, detail is not null);
    }

    [Theory]
    [InlineData(CatalogStatus.Inactive)]
    [InlineData(CatalogStatus.Archived)]
    public async Task Product_of_a_hidden_category_or_section_is_not_public(CatalogStatus hiddenStatus)
    {
        await SeedAsync(categoryStatus: hiddenStatus);
        var query = new CatalogQuery(_database.Context);
        Assert.Null(await query.FindProductAsync("cha"));

        await ResetAsync();
        await SeedAsync(sectionStatus: hiddenStatus);
        Assert.Null(await query.FindProductAsync("cha"));
    }

    [Fact]
    public async Task Archived_product_never_appears_in_any_public_listing()
    {
        await SeedAsync(productStatus: CatalogStatus.Archived);
        var query = new CatalogQuery(_database.Context);

        Assert.Empty((await query.SearchAsync(new ProductSearch())).Items);
        Assert.Empty((await query.SearchAsync(new ProductSearch(Query: "chá"))).Items);
        Assert.Empty(await query.FeaturedAsync(10));
        Assert.Empty(await query.BestSellersAsync(10));
        Assert.Empty(await query.NewArrivalsAsync(10));
        Assert.Empty(await query.BySectionAsync("chas", 10));
        Assert.Empty(await query.RelatedAsync("cha", 10));
        Assert.Equal(0, (await query.SectionsAsync()).Single().ProductCount);
    }

    private async Task ResetAsync()
    {
        _database.Context.Products.RemoveRange(_database.Context.Products);
        _database.Context.Categories.RemoveRange(_database.Context.Categories);
        _database.Context.Sections.RemoveRange(_database.Context.Sections);
        await _database.Context.SaveChangesAsync();
    }

    private async Task SeedAsync(CatalogStatus productStatus = CatalogStatus.Active, CatalogStatus categoryStatus = CatalogStatus.Active, CatalogStatus sectionStatus = CatalogStatus.Active)
    {
        var section = new Section { Name = "Chás", Slug = "chas", Status = sectionStatus };
        var category = new Category { Name = "Cafés", Slug = "cafes", Section = section, Status = categoryStatus };
        var product = new Product
        {
            Name = "Chá",
            Slug = "cha",
            Category = category,
            PriceMinor = 1000,
            Status = productStatus,
            IsFeatured = true,
            CreatedAt = Now,
            SalesCount = 3,
            Description = "Chá de ervas",
        };
        var sibling = new Product { Name = "Chá irmão", Slug = "cha-irmao", Category = category, PriceMinor = 1000, Status = productStatus, CreatedAt = Now };
        _database.Context.Products.AddRange(product, sibling);
        await _database.Context.SaveChangesAsync();
        _database.Context.ChangeTracker.Clear();
    }
}

public sealed class StockLevelsTests
{
    [Theory]
    [InlineData(-1, StockLevel.OutOfStock)]
    [InlineData(0, StockLevel.OutOfStock)]
    [InlineData(1, StockLevel.LowStock)]
    [InlineData(5, StockLevel.LowStock)]
    [InlineData(6, StockLevel.InStock)]
    public void Stock_level_follows_quantity(int quantity, StockLevel expected) =>
        Assert.Equal(expected, StockLevels.For(quantity));
}

public sealed class LocationDirectoryTests : IDisposable
{
    private readonly TestDatabase _database = new(withStoreData: true);
    private readonly LocationDirectory _directory;

    public LocationDirectoryTests()
    {
        _directory = new LocationDirectory(_database.Context);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Active_locations_come_in_sort_order_with_contacts()
    {
        var locations = await _directory.ActiveAsync();

        Assert.Equal(["vilanculos", "maputo-polana"], locations.Select(location => location.Slug));
        Assert.All(locations, location =>
        {
            Assert.Equal([ContactKind.Phone, ContactKind.WhatsApp, ContactKind.Email], location.Contacts.Select(contact => contact.Kind));
            Assert.NotEmpty(location.MapEmbedUrl);
            Assert.NotNull(location.Latitude);
        });
    }

    [Fact]
    public async Task Inactive_locations_are_hidden_from_the_directory_and_from_stock()
    {
        var maputo = await _database.Context.Locations.SingleAsync(location => location.Slug == "maputo-polana");
        maputo.Status = CatalogStatus.Inactive;
        await _database.Context.SaveChangesAsync();
        var productId = await _database.Context.Products.Where(product => product.Slug == "espresso").Select(product => product.Id).SingleAsync();

        var locations = await _directory.ActiveAsync();
        var stock = await _directory.StockForProductAsync(productId);

        Assert.Equal(["vilanculos"], locations.Select(location => location.Slug));
        Assert.Single(stock);
    }

    [Fact]
    public async Task Stock_per_location_shows_products_available_in_only_one_place()
    {
        var removed = await _database.Context.LocationStocks.OrderBy(stock => stock.ProductId).ThenByDescending(stock => stock.LocationId).FirstAsync();
        _database.Context.LocationStocks.Remove(removed);
        await _database.Context.SaveChangesAsync();
        var onlyOneLocation = removed.ProductId;

        var stock = await _directory.StockForProductAsync(onlyOneLocation);

        Assert.Single(stock);
        Assert.True(stock[0].Quantity > 0);
    }

    [Fact]
    public void Allocation_prefers_one_location_and_otherwise_splits_in_order()
    {
        LocationStock[] stocks = [new() { LocationId = 1, Quantity = 3 }, new() { LocationId = 2, Quantity = 10 }];

        var single = StockAllocator.Allocate(stocks, 5);
        var split = StockAllocator.Allocate(stocks, 12);
        var short12 = StockAllocator.Allocate(stocks, 14);

        Assert.Equal([new StockAllocation(2, 5)], single);
        Assert.Equal([new StockAllocation(1, 3), new StockAllocation(2, 9)], split);
        Assert.Null(short12);
    }
}
