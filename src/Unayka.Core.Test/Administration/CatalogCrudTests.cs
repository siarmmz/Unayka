using System.Text;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Administration;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Locations;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Administration;

public sealed class CatalogCrudTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();
    private readonly ManualTimeProvider _time = new(Now);
    private readonly InMemoryDigitalFileStore _files = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Product_saves_stock_per_location_and_only_tags_of_its_section()
    {
        var (categoryId, sectionId, otherSectionId) = await SeedSectionsAsync();
        var firstLocation = await SeedLocationAsync("vilanculos");
        var secondLocation = await SeedLocationAsync("maputo");
        var taxonomy = Taxonomy();
        var ownTag = (await taxonomy.SaveTagAsync(1, new TagForm { Name = "Calmante", Slug = "calmante", SectionId = sectionId })).EntityId!.Value;
        var foreignTag = (await taxonomy.SaveTagAsync(1, new TagForm { Name = "Hidratante", Slug = "hidratante", SectionId = otherSectionId })).EntityId!.Value;
        var form = new ProductForm
        {
            Name = "Chá",
            Slug = "cha",
            CategoryId = categoryId,
            Price = 10m,
            TagIds = [ownTag, foreignTag],
            Stocks = [new LocationStockForm { LocationId = firstLocation, Quantity = 4 }, new LocationStockForm { LocationId = secondLocation, Quantity = 9 }],
        };

        var saved = await Catalog().SaveProductAsync(1, form);
        var loaded = (await Catalog().GetProductAsync(saved.EntityId!.Value))!;

        Assert.True(saved.Succeeded);
        Assert.Equal([ownTag], loaded.TagIds);
        Assert.Equal(sectionId, loaded.SectionId);
        Assert.Equal([4, 9], loaded.Stocks.Select(stock => stock.Quantity));
    }

    [Fact]
    public async Task Product_validation_rejects_negative_stock_unknown_location_and_bad_slug()
    {
        var (categoryId, _, _) = await SeedSectionsAsync();
        var form = new ProductForm
        {
            Name = "Chá",
            Slug = "Slug Mau",
            CategoryId = categoryId,
            Price = 10m,
            Stocks = [new LocationStockForm { LocationId = 999, Quantity = -1 }],
        };

        var result = await Catalog().SaveProductAsync(1, form);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("stock", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("slug", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(_database.Context.Products);
    }

    [Fact]
    public async Task Digital_file_is_stored_privately_replaced_and_removed()
    {
        var (categoryId, _, _) = await SeedSectionsAsync();
        var product = await Catalog().SaveProductAsync(1, new ProductForm { Name = "Livro", Slug = "livro", CategoryId = categoryId, Price = 10m, Kind = ProductKind.Digital });

        var first = await Catalog().SetDigitalFileAsync(1, product.EntityId!.Value, Upload("a.pdf"));
        var firstKey = _files.Files.Keys.Single();
        var second = await Catalog().SetDigitalFileAsync(1, product.EntityId!.Value, Upload("b.pdf"));
        var loaded = (await Catalog().GetProductAsync(product.EntityId!.Value))!;

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.DoesNotContain(firstKey, _files.Files.Keys);
        Assert.Equal("b.pdf", loaded.DigitalFileName);

        await Catalog().RemoveDigitalFileAsync(1, product.EntityId!.Value);
        Assert.Empty(_files.Files);
        Assert.Empty(_database.Context.DigitalAssets);
    }

    [Fact]
    public async Task Digital_file_is_refused_for_physical_products_and_empty_files()
    {
        var (categoryId, _, _) = await SeedSectionsAsync();
        var product = await Catalog().SaveProductAsync(1, new ProductForm { Name = "Chá", Slug = "cha", CategoryId = categoryId, Price = 10m });

        var physical = await Catalog().SetDigitalFileAsync(1, product.EntityId!.Value, Upload("a.pdf"));
        var empty = await Catalog().SetDigitalFileAsync(1, product.EntityId!.Value, new DigitalFileUpload("a.pdf", "application/pdf", 0, new MemoryStream()));

        Assert.False(physical.Succeeded);
        Assert.False(empty.Succeeded);
        Assert.Empty(_files.Files);
    }

    [Fact]
    public async Task Section_category_and_tag_reject_invalid_and_duplicate_slugs()
    {
        var (_, sectionId, _) = await SeedSectionsAsync();
        var taxonomy = Taxonomy();

        var badSection = await taxonomy.SaveSectionAsync(1, new SectionForm { Name = "", Slug = "Mau Slug" });
        var duplicateSection = await taxonomy.SaveSectionAsync(1, new SectionForm { Name = "Outra", Slug = "chas" });
        var noSection = await taxonomy.SaveCategoryAsync(1, new CategoryForm { Name = "Cat", Slug = "cat", SectionId = 0 });
        var badTag = await taxonomy.SaveTagAsync(1, new TagForm { Name = "Tag", Slug = "Tag Mau", SectionId = sectionId });

        Assert.False(badSection.Succeeded);
        Assert.False(duplicateSection.Succeeded);
        Assert.False(noSection.Succeeded);
        Assert.False(badTag.Succeeded);
    }

    [Fact]
    public async Task Tags_of_different_sections_may_share_a_slug()
    {
        var (_, sectionId, otherSectionId) = await SeedSectionsAsync();
        var taxonomy = Taxonomy();

        var first = await taxonomy.SaveTagAsync(1, new TagForm { Name = "Suave", Slug = "suave", SectionId = sectionId });
        var second = await taxonomy.SaveTagAsync(1, new TagForm { Name = "Suave", Slug = "suave", SectionId = otherSectionId });
        var duplicate = await taxonomy.SaveTagAsync(1, new TagForm { Name = "Suave", Slug = "suave", SectionId = sectionId });

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.False(duplicate.Succeeded);
    }

    [Fact]
    public async Task Location_saves_contacts_with_normalized_phone_and_audits()
    {
        var form = new LocationForm
        {
            Name = "Unayka Maputo",
            Slug = "maputo",
            Latitude = -25.9,
            Longitude = 32.5,
            MapEmbedUrl = "https://www.openstreetmap.org/export/embed.html",
            Contacts =
            [
                new LocationContactForm { Kind = ContactKind.Phone, Label = "Chamadas", Value = "84 000 0201" },
                new LocationContactForm { Kind = ContactKind.WhatsApp, Label = "WhatsApp", Value = "+258840000202" },
                new LocationContactForm { Kind = ContactKind.Email, Label = "Email", Value = "Maputo@Unayka.co.mz" },
            ],
        };

        var saved = await Locations().SaveAsync(3, form);
        var loaded = (await Locations().GetAsync(saved.EntityId!.Value))!;

        Assert.True(saved.Succeeded);
        Assert.Equal(["+258 84 000 0201", "+258 84 000 0202", "maputo@unayka.co.mz"], loaded.Contacts.Select(contact => contact.Value));
        Assert.Contains(_database.Context.AuditEntries, entry => entry.Action == AdminAuditActions.LocationCreated && entry.ActorUserId == 3);
    }

    [Theory]
    [InlineData(ContactKind.Phone, "12345")]
    [InlineData(ContactKind.WhatsApp, "+351912345678")]
    [InlineData(ContactKind.Email, "sem-arroba")]
    [InlineData(ContactKind.Email, "ana@semponto")]
    public async Task Location_rejects_invalid_contacts(ContactKind kind, string value)
    {
        var form = new LocationForm { Name = "Loja", Slug = "loja", Contacts = [new LocationContactForm { Kind = kind, Label = "Contacto", Value = value }] };

        var result = await Locations().SaveAsync(1, form);

        Assert.False(result.Succeeded);
        Assert.Empty(_database.Context.Locations);
    }

    [Theory]
    [InlineData(-25.9, null, "https://example.org/map")]
    [InlineData(95.0, 32.5, "https://example.org/map")]
    [InlineData(-25.9, 200.0, "https://example.org/map")]
    [InlineData(-25.9, 32.5, "http://example.org/map")]
    public async Task Location_rejects_invalid_coordinates_and_insecure_map(double latitude, double? longitude, string mapUrl)
    {
        var form = new LocationForm { Name = "Loja", Slug = "loja", Latitude = latitude, Longitude = longitude, MapEmbedUrl = mapUrl };

        var result = await Locations().SaveAsync(1, form);

        Assert.False(result.Succeeded);
    }

    private static DigitalFileUpload Upload(string name) =>
        new(name, "application/pdf", 3, new MemoryStream(Encoding.ASCII.GetBytes("pdf")));

    private AdminCatalogService Catalog() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time), _files, _time);

    private AdminTaxonomyService Taxonomy() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time));

    private AdminLocationsService Locations() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time));

    private async Task<int> SeedLocationAsync(string slug)
    {
        await using var db = _database.Create();
        var location = new Location { Slug = slug, Name = slug };
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location.Id;
    }

    private async Task<(int CategoryId, int SectionId, int OtherSectionId)> SeedSectionsAsync()
    {
        await using var db = _database.Create();
        var category = new Category { Name = "Cafés", Slug = "cafes", Section = new Section { Name = "Chás", Slug = "chas" } };
        var other = new Section { Name = "Cosméticos", Slug = "cosmeticos" };
        db.Categories.Add(category);
        db.Sections.Add(other);
        await db.SaveChangesAsync();
        return (category.Id, category.SectionId, other.Id);
    }
}
