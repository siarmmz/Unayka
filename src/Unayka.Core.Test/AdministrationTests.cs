using System.Text;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Administration;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Coupons;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Orders;

namespace Unayka.Core.Test;

public sealed class AdminRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(OrderStatus.Paid, OrderStatus.Preparing, true)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Ready, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Paid, false)]
    public void Status_transitions_follow_the_workflow(OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, OrderStatusRules.CanMove(from, to));
    }

    [Fact]
    public void Allowed_targets_offer_next_step_and_cancel()
    {
        Assert.Equal([OrderStatus.Preparing, OrderStatus.Cancelled], OrderStatusRules.AllowedTargets(OrderStatus.Paid));
        Assert.Empty(OrderStatusRules.AllowedTargets(OrderStatus.Delivered));
    }

    [Fact]
    public void Dashboard_sums_only_paid_orders_per_period()
    {
        var orders = new[]
        {
            new OrderSample(Now, OrderStatus.Paid, 10000),
            new OrderSample(Now.AddHours(-1), OrderStatus.Delivered, 5000),
            new OrderSample(Now, OrderStatus.Cancelled, 99999),
            new OrderSample(Now, OrderStatus.AwaitingPayment, 7000),
            new OrderSample(Now.AddDays(-5), OrderStatus.Ready, 20000),
            new OrderSample(Now.AddDays(-40), OrderStatus.Paid, 80000),
        };

        var figures = DashboardCalculator.Compute(orders, Now);

        Assert.Equal(15000, figures.TodaySalesMinor);
        Assert.Equal(35000, figures.MonthSalesMinor);
        Assert.Equal(11666, figures.AverageTicketMinor);
        Assert.Equal(4, figures.PendingOrders);
    }

    [Fact]
    public void Dashboard_daily_series_covers_fourteen_days_with_zeros()
    {
        var figures = DashboardCalculator.Compute([new OrderSample(Now.AddDays(-2), OrderStatus.Paid, 3000)], Now);

        Assert.Equal(DashboardCalculator.ChartDays, figures.Daily.Count);
        Assert.Equal(LocalDays.ToLocalDay(Now), figures.Daily[^1].Day);
        Assert.Equal(3000, figures.Daily[^3].TotalMinor);
        Assert.Equal(0, figures.Daily[0].TotalMinor);
    }

    [Fact]
    public void Dashboard_with_no_orders_has_zero_average()
    {
        var figures = DashboardCalculator.Compute([], Now);

        Assert.Equal(0, figures.AverageTicketMinor);
        Assert.Equal(0, figures.MonthSalesMinor);
    }

    [Fact]
    public void Dashboard_groups_by_maputo_day()
    {
        var lateEvening = new DateTimeOffset(2026, 10, 11, 21, 30, 0, TimeSpan.Zero);

        var figures = DashboardCalculator.Compute([new OrderSample(lateEvening, OrderStatus.Paid, 1000)], Now);

        Assert.Equal(0, figures.TodaySalesMinor);
        Assert.Equal(1000, figures.Daily.Single(day => day.Day == LocalDays.ToLocalDay(lateEvening)).TotalMinor);
    }

    [Theory]
    [InlineData("Café Moído Especial", "cafe-moido-especial")]
    [InlineData("  Creme & Óleo  ", "creme-oleo")]
    [InlineData("---", "")]
    public void Slugs_are_built_from_names(string name, string expected)
    {
        Assert.Equal(expected, Slugs.FromName(name));
    }

    [Theory]
    [InlineData("cafe-moido", true)]
    [InlineData("Cafe", false)]
    [InlineData("cafe--moido", false)]
    [InlineData("", false)]
    public void Slug_validation(string slug, bool expected)
    {
        Assert.Equal(expected, Slugs.IsValid(slug));
    }

    [Fact]
    public void Money_input_rounds_to_minor_units()
    {
        Assert.Equal(12550, MoneyInput.ToMinor(125.5m));
        Assert.Equal(1, MoneyInput.ToMinor(0.005m));
        Assert.Equal(125.5m, MoneyInput.ToAmount(12550));
    }

    [Theory]
    [InlineData(0, CustomerSegment.NoPurchases)]
    [InlineData(1, CustomerSegment.New)]
    [InlineData(2, CustomerSegment.Recurring)]
    [InlineData(7, CustomerSegment.Recurring)]
    public void Customer_segment_depends_on_paid_orders(int paidOrders, CustomerSegment expected)
    {
        Assert.Equal(expected, CustomerSegments.For(paidOrders));
    }

    [Fact]
    public void Coupon_values_convert_between_input_and_storage()
    {
        Assert.Equal(1000, CouponValues.ToStoredValue(CouponKind.Percentage, 10m));
        Assert.Equal(5000, CouponValues.ToStoredValue(CouponKind.Fixed, 50m));
        Assert.Equal(10m, CouponValues.ToInputValue(CouponKind.Percentage, 1000));
    }

    [Fact]
    public void Coupon_validation_rejects_bad_input()
    {
        var form = new CouponForm { Code = "BAD CODE", Kind = CouponKind.Percentage, Value = 150 };

        Assert.Equal(2, CouponValues.Validate(form).Count);
    }
}

public sealed class AdminServicesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();
    private readonly ManualTimeProvider _time = new(Now);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Cancelling_an_order_restores_stock_and_audits()
    {
        var (productId, number) = await SeedOrderAsync(OrderStatus.Paid, quantity: 3, stock: 7);

        var result = await Orders().ChangeStatusAsync(5, number, OrderStatus.Cancelled);

        Assert.True(result.Succeeded);
        await using var db = _database.Create();
        Assert.Equal(10, await TestStock.TotalAsync(db, productId));
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.SingleAsync()).Status);
        var entry = await db.AuditEntries.SingleAsync();
        Assert.Equal(5, entry.ActorUserId);
        Assert.Equal(AdminAuditActions.OrderStatusChanged, entry.Action);
    }

    [Fact]
    public async Task Invalid_transition_changes_nothing()
    {
        var (_, number) = await SeedOrderAsync(OrderStatus.Paid, quantity: 1, stock: 4);

        var result = await Orders().ChangeStatusAsync(1, number, OrderStatus.Delivered);

        Assert.False(result.Succeeded);
        await using var db = _database.Create();
        Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync()).Status);
        Assert.Empty(db.AuditEntries);
    }

    [Fact]
    public async Task Order_list_filters_by_status_and_search()
    {
        await SeedOrderAsync(OrderStatus.Paid, quantity: 1, stock: 4);

        var matching = await Orders().ListAsync(new OrderFilter(OrderStatus.Paid, Search: "ana"), 1);
        var other = await Orders().ListAsync(new OrderFilter(OrderStatus.Delivered), 1);

        Assert.Single(matching.Items);
        Assert.Empty(other.Items);
    }

    [Fact]
    public async Task Saving_a_product_converts_price_and_rejects_duplicate_slug()
    {
        var categoryId = await SeedCategoryAsync();
        var catalog = Catalog();
        var form = new ProductForm { Name = "Café", Slug = "cafe", CategoryId = categoryId, Price = 125.5m };

        var created = await catalog.SaveProductAsync(1, form);
        var duplicate = await catalog.SaveProductAsync(1, new ProductForm { Name = "Outro", Slug = "cafe", CategoryId = categoryId, Price = 10m });

        Assert.True(created.Succeeded);
        Assert.False(duplicate.Succeeded);
        await using var db = _database.Create();
        Assert.Equal(12550, (await db.Products.SingleAsync()).PriceMinor);
        Assert.Equal(AdminAuditActions.ProductCreated, (await db.AuditEntries.SingleAsync()).Action);
    }

    [Fact]
    public async Task Editing_a_product_keeps_its_own_slug_valid()
    {
        var categoryId = await SeedCategoryAsync();
        var catalog = Catalog();
        var created = await catalog.SaveProductAsync(1, new ProductForm { Name = "Café", Slug = "cafe", CategoryId = categoryId, Price = 10m });

        var form = (await catalog.GetProductAsync(created.EntityId!.Value))!;
        form.Price = 12m;
        var updated = await catalog.SaveProductAsync(1, form);

        Assert.True(updated.Succeeded);
        Assert.Equal(1200, (await catalog.ListProductsAsync(ProductListView.All, null, 1)).Items.Single().PriceMinor);
    }

    [Fact]
    public async Task Coupon_revenue_sums_paid_orders_using_the_code()
    {
        await using (var db = _database.Create())
        {
            db.Coupons.Add(new Core.Coupons.Coupon { Code = "BEMVINDO10", Kind = Core.Coupons.CouponKind.Percentage, Value = 1000, CreatedAt = Now });
            db.Orders.AddRange(
                NewOrder("UN-000001", OrderStatus.Paid, 10000, "BEMVINDO10"),
                NewOrder("UN-000002", OrderStatus.Cancelled, 9000, "BEMVINDO10"),
                NewOrder("UN-000003", OrderStatus.Delivered, 5000, null));
            await db.SaveChangesAsync();
        }

        var rows = await new AdminCouponsService(new TestContextFactory(_database), new AuditLog(_database.Context, _time), _time).ListAsync();

        Assert.Equal(10000, rows.Single().RevenueMinor);
    }

    [Fact]
    public async Task Customers_report_totals_and_recurrence()
    {
        await using (var db = _database.Create())
        {
            var user = new User { Name = "Ana", Phone = "841234567", Role = UserRole.Customer, CreatedAt = Now };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var first = NewOrder("UN-000001", OrderStatus.Paid, 10000, null);
            var second = NewOrder("UN-000002", OrderStatus.Delivered, 5000, null);
            first.UserId = user.Id;
            second.UserId = user.Id;
            db.Orders.AddRange(first, second);
            await db.SaveChangesAsync();
        }

        var page = await new AdminCustomersService(new TestContextFactory(_database)).ListAsync(null, 1);

        var row = page.Items.Single();
        Assert.Equal(15000, row.TotalSpentMinor);
        Assert.Equal(CustomerSegment.Recurring, row.Segment);
    }

    [Fact]
    public async Task Dashboard_lists_low_stock_products()
    {
        var categoryId = await SeedCategoryAsync();
        await using (var db = _database.Create())
        {
            var location = new Location { Slug = "loja", Name = "Loja" };
            db.Products.AddRange(
                new Product { Name = "Baixo", Slug = "baixo", CategoryId = categoryId, Stocks = [new LocationStock { Location = location, Quantity = 2 }], PriceMinor = 100, CreatedAt = Now },
                new Product { Name = "Cheio", Slug = "cheio", CategoryId = categoryId, Stocks = [new LocationStock { Location = location, Quantity = 50 }], PriceMinor = 100, CreatedAt = Now });
            await db.SaveChangesAsync();
        }

        var data = await new AdminDashboardService(new TestContextFactory(_database), _time).LoadAsync();

        Assert.Equal("Baixo", data.LowStock.Single().Name);
    }

    private AdminOrdersService Orders() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time), _time);

    private AdminCatalogService Catalog() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time), new InMemoryDigitalFileStore(), _time);

    private async Task<int> SeedCategoryAsync()
    {
        await using var db = _database.Create();
        var category = new Category { Name = "Cafés", Slug = "cafes", Section = new Section { Name = "Chás", Slug = "chas" } };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private async Task<(int ProductId, string Number)> SeedOrderAsync(OrderStatus status, int quantity, int stock)
    {
        var categoryId = await SeedCategoryAsync();
        await using var db = _database.Create();
        var location = new Location { Slug = "loja", Name = "Loja" };
        var product = new Product { Name = "Café", Slug = "cafe", CategoryId = categoryId, Stocks = [new LocationStock { Location = location, Quantity = stock }], PriceMinor = 1000, CreatedAt = Now };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var order = NewOrder("UN-000001", status, 1000L * quantity, null);
        order.Items.Add(new OrderItem { ProductId = product.Id, LocationId = location.Id, ProductName = product.Name, UnitPriceMinor = 1000, Quantity = quantity });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (product.Id, order.Number);
    }

    private static Order NewOrder(string number, OrderStatus status, long total, string? couponCode) => new()
    {
        Number = number,
        CustomerName = "Ana Silva",
        CustomerPhone = "841234567",
        Status = status,
        TotalMinor = total,
        SubtotalMinor = total,
        CouponCode = couponCode,
        CreatedAt = Now,
        UpdatedAt = Now,
    };
}

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
