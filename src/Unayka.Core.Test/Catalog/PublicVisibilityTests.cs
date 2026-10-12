using Microsoft.EntityFrameworkCore;
using Unayka.Core.Catalog;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Catalog;

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
