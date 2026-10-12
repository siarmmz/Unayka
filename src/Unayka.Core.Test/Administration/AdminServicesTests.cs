using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Administration;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Orders;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Administration;

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
        Assert.Equal(10, (await db.Products.SingleAsync(product => product.Id == productId)).StockQuantity);
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
        var form = new ProductForm { Name = "Café", Slug = "cafe", CategoryId = categoryId, Price = 125.5m, StockQuantity = 3 };

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
        Assert.Equal(1200, (await catalog.ListProductsAsync(null, 1)).Items.Single().PriceMinor);
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
            db.Products.AddRange(
                new Product { Name = "Baixo", Slug = "baixo", CategoryId = categoryId, StockQuantity = 2, PriceMinor = 100, CreatedAt = Now },
                new Product { Name = "Cheio", Slug = "cheio", CategoryId = categoryId, StockQuantity = 50, PriceMinor = 100, CreatedAt = Now });
            await db.SaveChangesAsync();
        }

        var data = await new AdminDashboardService(new TestContextFactory(_database), _time).LoadAsync();

        Assert.Equal("Baixo", data.LowStock.Single().Name);
    }

    private AdminOrdersService Orders() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time), _time);

    private AdminCatalogService Catalog() => new(new TestContextFactory(_database), new AuditLog(_database.Context, _time), _time);

    private async Task<int> SeedCategoryAsync()
    {
        await using var db = _database.Create();
        var category = new Category { Name = "Cafés", Slug = "cafes" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private async Task<(int ProductId, string Number)> SeedOrderAsync(OrderStatus status, int quantity, int stock)
    {
        var categoryId = await SeedCategoryAsync();
        await using var db = _database.Create();
        var product = new Product { Name = "Café", Slug = "cafe", CategoryId = categoryId, StockQuantity = stock, PriceMinor = 1000, CreatedAt = Now };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var order = NewOrder("UN-000001", status, 1000L * quantity, null);
        order.Items.Add(new OrderItem { ProductId = product.Id, ProductName = product.Name, UnitPriceMinor = 1000, Quantity = quantity });
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
