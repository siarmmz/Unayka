using Microsoft.EntityFrameworkCore;
using Unayka.Core.Administration;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Orders;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Administration;

public sealed class ArchiveRuleTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();
    private readonly ManualTimeProvider _time = new(Now);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Ordered_product_cannot_be_deleted_but_can_be_archived()
    {
        var (productId, _, _, _) = await SeedOrderedProductAsync();

        var deleted = await Catalog().DeleteProductAsync(1, productId);
        var archived = await Catalog().SetProductStatusAsync(1, productId, CatalogStatus.Archived);

        Assert.False(deleted.Succeeded);
        Assert.True(archived.Succeeded);
        var row = (await Catalog().ListProductsAsync(ProductListView.Archived, null, 1)).Items.Single();
        Assert.False(row.CanDelete);
        Assert.Equal(CatalogStatus.Archived, row.Status);
    }

    [Fact]
    public async Task Unreferenced_product_is_deleted_with_its_cart_lines_and_audited()
    {
        var (categoryId, _) = await SeedCategoryAsync();
        var created = await Catalog().SaveProductAsync(1, NewProduct("Chá", "cha", categoryId));
        await using (var db = _database.Create())
        {
            var cart = new Core.Carts.Cart { Id = Guid.NewGuid(), UpdatedAt = Now };
            cart.Items.Add(new Core.Carts.CartItem { ProductId = created.EntityId!.Value, Quantity = 1 });
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
        }

        var result = await Catalog().DeleteProductAsync(7, created.EntityId!.Value);

        Assert.True(result.Succeeded);
        await using var check = _database.Create();
        Assert.Empty(check.Products);
        Assert.Empty(check.CartItems);
        Assert.Contains(check.AuditEntries, entry => entry.Action == AdminAuditActions.ProductDeleted && entry.ActorUserId == 7);
    }

    [Fact]
    public async Task Product_list_views_split_by_flag_and_status()
    {
        var (categoryId, _) = await SeedCategoryAsync();
        var catalog = Catalog();
        await catalog.SaveProductAsync(1, NewProduct("Normal", "normal", categoryId));
        await catalog.SaveProductAsync(1, NewProduct("Novo", "novo", categoryId, form => form.IsNew = true));
        await catalog.SaveProductAsync(1, NewProduct("Destaque", "destaque", categoryId, form => form.IsFeatured = true));
        await catalog.SaveProductAsync(1, NewProduct("Inativo", "inativo", categoryId, form => form.Status = CatalogStatus.Inactive));
        await catalog.SaveProductAsync(1, NewProduct("Arquivado", "arquivado", categoryId, form => form.Status = CatalogStatus.Archived));

        Assert.Equal(["Destaque", "Inativo", "Normal", "Novo"], await Names(catalog, ProductListView.All));
        Assert.Equal(["Novo"], await Names(catalog, ProductListView.New));
        Assert.Equal(["Destaque"], await Names(catalog, ProductListView.Featured));
        Assert.Equal(["Inativo"], await Names(catalog, ProductListView.Inactive));
        Assert.Equal(["Arquivado"], await Names(catalog, ProductListView.Archived));
    }

    [Fact]
    public async Task Category_with_products_cannot_be_deleted_but_empty_one_can()
    {
        var (categoryId, sectionId) = await SeedCategoryAsync();
        await Catalog().SaveProductAsync(1, NewProduct("Chá", "cha", categoryId));
        var taxonomy = Taxonomy();
        var empty = await taxonomy.SaveCategoryAsync(1, new CategoryForm { Name = "Vazia", Slug = "vazia", SectionId = sectionId });

        var blocked = await taxonomy.DeleteCategoryAsync(1, categoryId);
        var removed = await taxonomy.DeleteCategoryAsync(1, empty.EntityId!.Value);
        var rows = await taxonomy.ListCategoriesAsync();

        Assert.False(blocked.Succeeded);
        Assert.True(removed.Succeeded);
        Assert.False(rows.Single().CanDelete);
    }

    [Fact]
    public async Task Section_with_categories_cannot_be_deleted_but_empty_one_can()
    {
        var (_, sectionId) = await SeedCategoryAsync();
        var taxonomy = Taxonomy();
        var empty = await taxonomy.SaveSectionAsync(1, new SectionForm { Name = "Livros", Slug = "livros" });

        var blocked = await taxonomy.DeleteSectionAsync(1, sectionId);
        var archived = await taxonomy.SetSectionStatusAsync(1, sectionId, CatalogStatus.Archived);
        var removed = await taxonomy.DeleteSectionAsync(1, empty.EntityId!.Value);

        Assert.False(blocked.Succeeded);
        Assert.True(archived.Succeeded);
        Assert.True(removed.Succeeded);
    }

    [Fact]
    public async Task Deleting_a_tag_unlinks_it_from_products()
    {
        var (categoryId, sectionId) = await SeedCategoryAsync();
        var taxonomy = Taxonomy();
        var tag = await taxonomy.SaveTagAsync(1, new TagForm { Name = "Calmante", Slug = "calmante", SectionId = sectionId });
        await Catalog().SaveProductAsync(1, NewProduct("Chá", "cha", categoryId, form => form.TagIds = [tag.EntityId!.Value]));

        var result = await taxonomy.DeleteTagAsync(1, tag.EntityId!.Value);

        Assert.True(result.Succeeded);
        await using var db = _database.Create();
        Assert.Empty((await db.Products.Include(product => product.Tags).SingleAsync()).Tags);
    }

    [Fact]
    public async Task Location_with_orders_cannot_be_deleted_but_unreferenced_one_can()
    {
        var (_, _, locationId, _) = await SeedOrderedProductAsync();
        var locations = Locations();
        var spare = await locations.SaveAsync(1, new LocationForm { Name = "Reserva", Slug = "reserva" });

        var blocked = await locations.DeleteAsync(1, locationId);
        var removed = await locations.DeleteAsync(1, spare.EntityId!.Value);
        var rows = await locations.ListAsync();

        Assert.False(blocked.Succeeded);
        Assert.True(removed.Succeeded);
        Assert.False(rows.Single().CanDelete);
    }

    [Fact]
    public async Task Every_status_change_and_delete_is_audited()
    {
        var (categoryId, sectionId) = await SeedCategoryAsync();
        var created = await Catalog().SaveProductAsync(1, NewProduct("Chá", "cha", categoryId));

        await Catalog().SetProductStatusAsync(1, created.EntityId!.Value, CatalogStatus.Inactive);
        await Taxonomy().SetCategoryStatusAsync(1, categoryId, CatalogStatus.Archived);
        await Taxonomy().SetSectionStatusAsync(1, sectionId, CatalogStatus.Inactive);

        await using var db = _database.Create();
        var actions = await db.AuditEntries.Select(entry => entry.Action).ToListAsync();
        Assert.Contains(AdminAuditActions.ProductStatusChanged, actions);
        Assert.Contains(AdminAuditActions.CategoryStatusChanged, actions);
        Assert.Contains(AdminAuditActions.SectionStatusChanged, actions);
    }

    private static async Task<List<string>> Names(AdminCatalogService catalog, ProductListView view) =>
        (await catalog.ListProductsAsync(view, null, 1)).Items.Select(item => item.Name).ToList();

    private static ProductForm NewProduct(string name, string slug, int categoryId, Action<ProductForm>? customize = null)
    {
        var form = new ProductForm { Name = name, Slug = slug, CategoryId = categoryId, Price = 10m };
        customize?.Invoke(form);
        return form;
    }

    private AdminCatalogService Catalog() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time), new InMemoryDigitalFileStore(), _time);

    private AdminTaxonomyService Taxonomy() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time));

    private AdminLocationsService Locations() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time));

    private async Task<(int CategoryId, int SectionId)> SeedCategoryAsync()
    {
        await using var db = _database.Create();
        var category = new Category { Name = "Cafés", Slug = "cafes", Section = new Section { Name = "Chás", Slug = "chas" } };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return (category.Id, category.SectionId);
    }

    private async Task<(int ProductId, int CategoryId, int LocationId, int SectionId)> SeedOrderedProductAsync()
    {
        var (categoryId, sectionId) = await SeedCategoryAsync();
        await using var db = _database.Create();
        var location = new Location { Slug = "loja", Name = "Loja" };
        var product = new Product { Name = "Café", Slug = "cafe", CategoryId = categoryId, PriceMinor = 1000, CreatedAt = Now, Stocks = [new LocationStock { Location = location, Quantity = 5 }] };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var order = new Order { Number = "UN-000001", CustomerName = "Ana", CustomerPhone = "841234567", Status = OrderStatus.Paid, TotalMinor = 1000, SubtotalMinor = 1000, CreatedAt = Now, UpdatedAt = Now };
        order.Items.Add(new OrderItem { ProductId = product.Id, LocationId = location.Id, ProductName = product.Name, UnitPriceMinor = 1000, Quantity = 1 });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (product.Id, categoryId, location.Id, sectionId);
    }
}
