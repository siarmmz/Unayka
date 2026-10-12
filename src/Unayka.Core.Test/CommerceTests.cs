using Microsoft.EntityFrameworkCore;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Coupons;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Test;

public sealed class CartServiceTests : IDisposable
{
    private readonly CheckoutFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Adding_creates_a_cart_and_totals_use_current_prices()
    {
        var product = await _fixture.FirstProductAsync();

        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 2);
        var first = await _fixture.Carts.GetAsync(change.CartId);
        product.PriceMinor += 1_000;
        await _fixture.Db.SaveChangesAsync();
        var second = await _fixture.Carts.GetAsync(change.CartId);

        Assert.Equal(CartChange.Added, change.Change);
        Assert.Equal(2 * (product.PriceMinor - 1_000), first.SubtotalMinor);
        Assert.Equal(2 * product.PriceMinor, second.SubtotalMinor);
    }

    [Fact]
    public async Task Adding_the_same_product_twice_merges_into_one_line()
    {
        var product = await _fixture.FirstProductAsync();
        var first = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        await _fixture.Carts.AddAsync(first.CartId, null, product.Id, 2);
        var view = await _fixture.Carts.GetAsync(first.CartId);

        Assert.Single(view.Lines);
        Assert.Equal(3, view.ItemCount);
    }

    [Fact]
    public async Task Quantity_is_limited_by_stock()
    {
        var product = await _fixture.FirstProductAsync();
        await TestStock.SetTotalAsync(_fixture.Db, product.Id, 3);

        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 10);
        var view = await _fixture.Carts.GetAsync(change.CartId);

        Assert.Equal(CartChange.LimitedByStock, change.Change);
        Assert.Equal(3, view.ItemCount);
    }

    [Fact]
    public async Task Out_of_stock_product_is_not_added()
    {
        var product = await _fixture.FirstProductAsync();
        await TestStock.SetTotalAsync(_fixture.Db, product.Id, 0);

        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        Assert.Equal(CartChange.OutOfStock, change.Change);
        Assert.True((await _fixture.Carts.GetAsync(change.CartId)).IsEmpty);
    }

    [Fact]
    public async Task Unknown_product_is_reported()
    {
        var change = await _fixture.Carts.AddAsync(null, null, 99_999, 1);

        Assert.Equal(CartChange.UnknownProduct, change.Change);
    }

    [Fact]
    public async Task Setting_zero_quantity_removes_the_line()
    {
        var product = await _fixture.FirstProductAsync();
        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 2);

        await _fixture.Carts.SetQuantityAsync(change.CartId, product.Id, 0);

        Assert.True((await _fixture.Carts.GetAsync(change.CartId)).IsEmpty);
    }

    [Fact]
    public async Task Valid_coupon_is_applied_and_discounts_the_total()
    {
        var product = await _fixture.FirstProductAsync();
        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        var result = await _fixture.Carts.ApplyCouponAsync(change.CartId, " bemvindo10 ");
        var view = await _fixture.Carts.GetAsync(change.CartId);

        Assert.Equal(CouponApplyOutcome.Applied, result.Outcome);
        Assert.Equal("BEMVINDO10", view.CouponCode);
        Assert.Equal(view.SubtotalMinor / 10, view.DiscountMinor);
        Assert.Equal(view.SubtotalMinor - view.DiscountMinor, view.TotalMinor);
    }

    [Fact]
    public async Task Expired_coupon_is_rejected_and_not_stored()
    {
        var product = await _fixture.FirstProductAsync();
        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 1);
        _fixture.Db.Coupons.Add(new Coupons.Coupon
        {
            Code = "NATAL2025",
            Description = "Campanha terminada",
            Kind = Coupons.CouponKind.Percentage,
            Value = 1500,
            EndsAt = _fixture.Time.GetUtcNow().AddDays(-1),
            CreatedAt = _fixture.Time.GetUtcNow(),
        });
        await _fixture.Db.SaveChangesAsync();

        var result = await _fixture.Carts.ApplyCouponAsync(change.CartId, "NATAL2025");

        Assert.Equal(CouponApplyOutcome.Rejected, result.Outcome);
        Assert.Equal(CouponRejection.Expired, result.Rejection);
        Assert.Null((await _fixture.Carts.GetAsync(change.CartId)).CouponCode);
    }

    [Fact]
    public async Task Unknown_coupon_code_is_reported()
    {
        var product = await _fixture.FirstProductAsync();
        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        var result = await _fixture.Carts.ApplyCouponAsync(change.CartId, "NOPE");

        Assert.Equal(CouponApplyOutcome.Unknown, result.Outcome);
    }

    [Fact]
    public async Task Clearing_the_cart_removes_lines_and_coupon()
    {
        var product = await _fixture.FirstProductAsync();
        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 1);
        await _fixture.Carts.ApplyCouponAsync(change.CartId, "BEMVINDO10");

        await _fixture.Carts.ClearAsync(change.CartId);
        var view = await _fixture.Carts.GetAsync(change.CartId);

        Assert.True(view.IsEmpty);
        Assert.Null(view.CouponCode);
    }
}

public sealed class CouponEvaluatorTests
{
    private const int CoffeeCategory = 1;
    private const int SkinCategory = 2;

    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly CouponLine[] Lines = [new(CoffeeCategory, 20_000), new(SkinCategory, 10_000)];

    [Fact]
    public void Percentage_coupon_discounts_basis_points_of_the_subtotal()
    {
        var result = CouponEvaluator.Evaluate(Percentage(1000), Lines, Now);

        Assert.True(result.IsAccepted);
        Assert.Equal(3_000, result.DiscountMinor);
    }

    [Fact]
    public void Fixed_coupon_discounts_the_fixed_value()
    {
        var result = CouponEvaluator.Evaluate(Fixed(5_000), Lines, Now);

        Assert.Equal(5_000, result.DiscountMinor);
    }

    [Fact]
    public void Fixed_coupon_never_exceeds_the_subtotal()
    {
        var result = CouponEvaluator.Evaluate(Fixed(99_000), Lines, Now);

        Assert.Equal(30_000, result.DiscountMinor);
    }

    [Fact]
    public void Inactive_coupon_is_rejected()
    {
        var coupon = Percentage(1000);
        coupon.IsActive = false;

        Assert.Equal(CouponRejection.Inactive, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_before_its_start_is_rejected()
    {
        var coupon = Percentage(1000);
        coupon.StartsAt = Now.AddDays(1);

        Assert.Equal(CouponRejection.NotStarted, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_after_its_end_is_rejected()
    {
        var coupon = Percentage(1000);
        coupon.EndsAt = Now.AddMinutes(-1);

        Assert.Equal(CouponRejection.Expired, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_inside_its_window_is_accepted()
    {
        var coupon = Percentage(1000);
        coupon.StartsAt = Now.AddDays(-1);
        coupon.EndsAt = Now.AddDays(1);

        Assert.True(CouponEvaluator.Evaluate(coupon, Lines, Now).IsAccepted);
    }

    [Fact]
    public void Coupon_that_reached_its_usage_limit_is_rejected()
    {
        var coupon = Percentage(1000);
        coupon.UsageLimit = 3;
        coupon.UsedCount = 3;

        Assert.Equal(CouponRejection.UsageLimitReached, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_below_its_usage_limit_is_accepted()
    {
        var coupon = Percentage(1000);
        coupon.UsageLimit = 3;
        coupon.UsedCount = 2;

        Assert.True(CouponEvaluator.Evaluate(coupon, Lines, Now).IsAccepted);
    }

    [Fact]
    public void Coupon_with_a_higher_minimum_subtotal_is_rejected()
    {
        var coupon = Fixed(5_000);
        coupon.MinSubtotalMinor = 30_001;

        Assert.Equal(CouponRejection.MinSubtotalNotReached, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_exactly_at_its_minimum_subtotal_is_accepted()
    {
        var coupon = Fixed(5_000);
        coupon.MinSubtotalMinor = 30_000;

        Assert.True(CouponEvaluator.Evaluate(coupon, Lines, Now).IsAccepted);
    }

    [Fact]
    public void Category_coupon_only_discounts_lines_of_that_category()
    {
        var coupon = Percentage(5000);
        coupon.CategoryId = CoffeeCategory;

        Assert.Equal(10_000, CouponEvaluator.Evaluate(coupon, Lines, Now).DiscountMinor);
    }

    [Fact]
    public void Category_coupon_without_matching_lines_is_rejected()
    {
        var coupon = Percentage(5000);
        coupon.CategoryId = 99;

        Assert.Equal(CouponRejection.CategoryNotInCart, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Fixed_category_coupon_is_capped_by_the_category_subtotal()
    {
        var coupon = Fixed(15_000);
        coupon.CategoryId = SkinCategory;

        Assert.Equal(10_000, CouponEvaluator.Evaluate(coupon, Lines, Now).DiscountMinor);
    }

    [Fact]
    public void Minimum_subtotal_counts_the_whole_cart_even_for_category_coupons()
    {
        var coupon = Fixed(1_000);
        coupon.CategoryId = SkinCategory;
        coupon.MinSubtotalMinor = 25_000;

        Assert.True(CouponEvaluator.Evaluate(coupon, Lines, Now).IsAccepted);
    }

    [Fact]
    public void Inactive_is_reported_before_expired()
    {
        var coupon = Percentage(1000);
        coupon.IsActive = false;
        coupon.EndsAt = Now.AddDays(-1);

        Assert.Equal(CouponRejection.Inactive, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Empty_cart_is_rejected_by_the_minimum_or_the_category_rule()
    {
        var result = CouponEvaluator.Evaluate(Percentage(1000), [], Now);

        Assert.Equal(CouponRejection.CategoryNotInCart, result.Rejection);
    }

    private static Coupon Percentage(long basisPoints) =>
        new() { Code = "TEST", Kind = CouponKind.Percentage, Value = basisPoints };

    private static Coupon Fixed(long minor) =>
        new() { Code = "TEST", Kind = CouponKind.Fixed, Value = minor };
}

public sealed class DigitalDownloadTests : IDisposable
{
    private const string Phone = "841234567";
    private const int Buyer = 7;
    private const int Stranger = 8;

    private readonly CheckoutFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task The_buyer_of_a_paid_order_downloads_and_each_download_is_counted_and_audited()
    {
        var itemId = await PlaceDigitalOrderAsync(OrderStatus.Paid);

        var first = await _fixture.Downloads.OpenAsync(itemId, Buyer);
        var second = await _fixture.Downloads.OpenAsync(itemId, Buyer);

        Assert.Equal(DownloadOutcome.Granted, first.Outcome);
        Assert.Equal(DownloadOutcome.Granted, second.Outcome);
        Assert.NotNull(first.File);
        await first.File.Content.DisposeAsync();
        await second.File!.Content.DisposeAsync();
        var grant = await _fixture.Db.DigitalDownloadGrants.SingleAsync();
        Assert.Equal(2, grant.DownloadCount);
        Assert.Equal(Buyer, grant.UserId);
        Assert.Equal(2, await _fixture.Db.AuditEntries.CountAsync(entry => entry.Action == "download.served"));
    }

    [Fact]
    public async Task Another_user_is_refused()
    {
        var itemId = await PlaceDigitalOrderAsync(OrderStatus.Paid);

        var result = await _fixture.Downloads.OpenAsync(itemId, Stranger);

        Assert.Equal(DownloadOutcome.NotFound, result.Outcome);
        Assert.Null(result.File);
        Assert.Empty(_fixture.Db.DigitalDownloadGrants);
    }

    [Fact]
    public async Task An_anonymous_visitor_is_refused()
    {
        var itemId = await PlaceDigitalOrderAsync(OrderStatus.Paid);

        var result = await _fixture.Downloads.OpenAsync(itemId, null);

        Assert.Equal(DownloadOutcome.NotSignedIn, result.Outcome);
        Assert.Null(result.File);
    }

    [Fact]
    public async Task An_order_that_is_not_paid_yet_is_refused()
    {
        var itemId = await PlaceDigitalOrderAsync(OrderStatus.AwaitingPayment);

        var result = await _fixture.Downloads.OpenAsync(itemId, Buyer);

        Assert.Equal(DownloadOutcome.NotPaid, result.Outcome);
    }

    [Fact]
    public async Task A_physical_line_is_not_downloadable()
    {
        var product = (await _fixture.PhysicalProductsAsync(1))[0];
        var cart = await _fixture.Carts.AddAsync(null, Buyer, product.Id, 1);
        var placed = await _fixture.Placement.PlaceAsync(Request(cart.CartId));
        var order = await _fixture.Db.Orders.Include(candidate => candidate.Items).SingleAsync(candidate => candidate.Number == placed.OrderNumber);
        order.Status = OrderStatus.Paid;
        await _fixture.Db.SaveChangesAsync();

        var result = await _fixture.Downloads.OpenAsync(order.Items.Single().Id, Buyer);

        Assert.Equal(DownloadOutcome.NotFound, result.Outcome);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.AwaitingPayment, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Only_paid_orders_allow_downloads(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderPayments.IsPaid(status));
    }

    private async Task<int> PlaceDigitalOrderAsync(OrderStatus status)
    {
        var ebook = await _fixture.EbookAsync();
        var cart = await _fixture.Carts.AddAsync(null, Buyer, ebook.Id, 1);
        var placed = await _fixture.Placement.PlaceAsync(Request(cart.CartId));
        var order = await _fixture.Db.Orders.Include(candidate => candidate.Items).SingleAsync(candidate => candidate.Number == placed.OrderNumber);
        order.Status = status;
        await _fixture.Db.SaveChangesAsync();
        return order.Items.Single().Id;
    }

    private static PlaceOrderRequest Request(Guid? cartId) =>
        new(cartId, Buyer, "Ana Silva", Phone, null, null, PaymentMethod.MPesa);
}

public sealed class OrderPlacementTests : IDisposable
{
    private const string Phone = "841234567";

    private readonly CheckoutFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Placing_an_order_snapshots_lines_and_reserves_stock()
    {
        var product = await _fixture.FirstProductAsync();
        var stockBefore = await TestStock.TotalAsync(_fixture.Db, product.Id);
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 2);
        await _fixture.Carts.ApplyCouponAsync(cart.CartId, "BEMVINDO10");

        var result = await _fixture.Placement.PlaceAsync(Request(cart.CartId));

        Assert.True(result.Succeeded);
        Assert.Matches(@"^UN-\d{6}$", result.OrderNumber);
        _fixture.Db.ChangeTracker.Clear();
        var order = await _fixture.Db.Orders.Include(candidate => candidate.Items).Include(candidate => candidate.Payments).SingleAsync(candidate => candidate.Number == result.OrderNumber);
        var subtotal = 2 * product.PriceMinor;
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(OrderChannel.Online, order.Channel);
        Assert.Equal(subtotal, order.SubtotalMinor);
        Assert.Equal(subtotal / 10, order.DiscountMinor);
        Assert.Equal(0, order.DeliveryFeeMinor);
        Assert.Equal(subtotal - subtotal / 10, order.TotalMinor);
        Assert.Equal(product.Name, order.Items.Single().ProductName);
        Assert.Equal(PaymentStatus.Pending, order.Payments.Single().Status);
        Assert.Equal(stockBefore - 2, await TestStock.TotalAsync(_fixture.Db, product.Id));
        Assert.Equal(ProductKind.Physical, order.Items.Single().Kind);
        Assert.NotNull(order.Items.Single().LocationId);
        Assert.Equal(1, (await _fixture.Db.Coupons.SingleAsync(coupon => coupon.Code == "BEMVINDO10")).UsedCount);
        Assert.True((await _fixture.Carts.GetAsync(cart.CartId)).IsEmpty);
        Assert.True(await _fixture.Db.AuditEntries.AnyAsync(entry => entry.Action == "order.placed" && entry.EntityId == result.OrderNumber));
    }

    [Fact]
    public async Task Digital_lines_skip_stock_and_have_no_location()
    {
        var ebook = await _fixture.Db.Products.SingleAsync(candidate => candidate.Slug == "e-book-receitas-com-cha");
        var cart = await _fixture.Carts.AddAsync(null, 7, ebook.Id, 1);

        var result = await _fixture.Placement.PlaceAsync(Request(cart.CartId, 7));

        Assert.True(result.Succeeded);
        var item = (await _fixture.Db.Orders.Include(order => order.Items).SingleAsync(order => order.Number == result.OrderNumber)).Items.Single();
        Assert.Equal(ProductKind.Digital, item.Kind);
        Assert.Null(item.LocationId);
        Assert.False(string.IsNullOrEmpty(item.ImageUrl));
    }

    [Fact]
    public async Task Order_numbers_start_at_one_and_increase_by_one()
    {
        var generator = new OrderNumberGenerator(_fixture.Db);
        var product = await _fixture.FirstProductAsync();
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        var first = await generator.NextAsync();
        await _fixture.Placement.PlaceAsync(Request(cart.CartId));
        var second = await generator.NextAsync();

        Assert.Equal("UN-000001", first);
        Assert.Equal("UN-000002", second);
    }

    [Fact]
    public async Task Totals_ignore_anything_but_the_server_cart()
    {
        var product = await _fixture.FirstProductAsync();
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        var result = await _fixture.Placement.PlaceAsync(Request(cart.CartId));

        var order = await _fixture.Db.Orders.SingleAsync(candidate => candidate.Number == result.OrderNumber);
        Assert.Equal(product.PriceMinor, order.TotalMinor);
        Assert.Equal(0, order.DeliveryFeeMinor);
    }

    [Fact]
    public async Task Insufficient_stock_fails_without_changing_anything()
    {
        var product = await _fixture.FirstProductAsync();
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 2);
        await TestStock.SetTotalAsync(_fixture.Db, product.Id, 1);
        var ordersBefore = await _fixture.Db.Orders.CountAsync();

        var result = await _fixture.Placement.PlaceAsync(Request(cart.CartId));

        Assert.Equal(PlaceOrderOutcome.OutOfStock, result.Outcome);
        Assert.StartsWith(product.Name, result.Detail, StringComparison.Ordinal);
        Assert.Equal(ordersBefore, await _fixture.Db.Orders.CountAsync());
        Assert.False((await _fixture.Carts.GetAsync(cart.CartId)).IsEmpty);
    }

    [Fact]
    public async Task Coupon_that_became_invalid_blocks_the_order()
    {
        var product = await _fixture.FirstProductAsync();
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 1);
        await _fixture.Carts.ApplyCouponAsync(cart.CartId, "BEMVINDO10");
        var coupon = await _fixture.Db.Coupons.SingleAsync(candidate => candidate.Code == "BEMVINDO10");
        coupon.IsActive = false;
        await _fixture.Db.SaveChangesAsync();

        var result = await _fixture.Placement.PlaceAsync(Request(cart.CartId));

        Assert.Equal(PlaceOrderOutcome.CouponNoLongerValid, result.Outcome);
    }

    [Fact]
    public async Task Empty_cart_is_rejected()
    {
        var result = await _fixture.Placement.PlaceAsync(Request(null));

        Assert.Equal(PlaceOrderOutcome.EmptyCart, result.Outcome);
    }

    [Theory]
    [InlineData("", Phone, PlaceOrderOutcome.InvalidName)]
    [InlineData("Ana", "123", PlaceOrderOutcome.InvalidPhone)]
    public async Task Invalid_input_is_rejected(string name, string phone, PlaceOrderOutcome expected)
    {
        var request = new PlaceOrderRequest(
            null, null, name, phone, null, null, PaymentMethod.MPesa);

        var result = await _fixture.Placement.PlaceAsync(request);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task Invalid_email_is_rejected()
    {
        var request = Request(null) with { Email = "not an email" };

        var result = await _fixture.Placement.PlaceAsync(request);

        Assert.Equal(PlaceOrderOutcome.InvalidEmail, result.Outcome);
    }

    [Fact]
    public void Only_staff_owner_or_placing_browser_can_view_an_order()
    {
        var order = new OrderDetail("UN-000900", 7, OrderStatus.Paid, FulfillmentKind.Pickup, "Ana", Phone, null, null, 0, 0, 0, 0, null, DateTimeOffset.UnixEpoch, [], null);

        Assert.True(OrderAccess.CanView(order, 7, false, []));
        Assert.True(OrderAccess.CanView(order, null, true, []));
        Assert.True(OrderAccess.CanView(order, null, false, ["UN-000900"]));
        Assert.False(OrderAccess.CanView(order, 8, false, ["UN-000001"]));
        Assert.False(OrderAccess.CanView(order, null, false, []));
    }

    private static PlaceOrderRequest Request(Guid? cartId, int? userId = null) =>
        new(cartId, userId, "Ana Silva", Phone, null, null, PaymentMethod.MPesa);
}

public sealed class PickupLocationTests : IDisposable
{
    private const string Phone = "841234567";
    private const int Buyer = 7;

    private readonly CheckoutFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task A_new_line_defaults_to_the_first_location_with_stock()
    {
        var product = (await _fixture.PhysicalProductsAsync(1))[0];
        var locations = await _fixture.LocationIdsAsync();
        await _fixture.SetStockAsync(product.Id, locations[0], 0);
        await _fixture.SetStockAsync(product.Id, locations[1], 5);

        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 2);
        var line = (await _fixture.Carts.GetAsync(change.CartId)).Lines.Single();

        Assert.Equal(locations[1], line.LocationId);
        Assert.False(line.ExceedsStock);
    }

    [Fact]
    public async Task Choosing_a_location_validates_stock_there_and_limits_the_quantity()
    {
        var product = (await _fixture.PhysicalProductsAsync(1))[0];
        var locations = await _fixture.LocationIdsAsync();
        await _fixture.SetStockAsync(product.Id, locations[0], 10);
        await _fixture.SetStockAsync(product.Id, locations[1], 3);
        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 6);

        var moved = await _fixture.Carts.SetLocationAsync(change.CartId, product.Id, locations[1]);
        var line = (await _fixture.Carts.GetAsync(change.CartId)).Lines.Single();

        Assert.Equal(CartChange.LimitedByStock, moved);
        Assert.Equal(locations[1], line.LocationId);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(2, line.LocationOptions.Count);
    }

    [Fact]
    public async Task A_location_without_stock_is_refused_and_the_line_keeps_its_location()
    {
        var product = (await _fixture.PhysicalProductsAsync(1))[0];
        var locations = await _fixture.LocationIdsAsync();
        await _fixture.SetStockAsync(product.Id, locations[0], 4);
        await _fixture.SetStockAsync(product.Id, locations[1], 0);
        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        var moved = await _fixture.Carts.SetLocationAsync(change.CartId, product.Id, locations[1]);
        var unknown = await _fixture.Carts.SetLocationAsync(change.CartId, product.Id, 99_999);
        var line = (await _fixture.Carts.GetAsync(change.CartId)).Lines.Single();

        Assert.Equal(CartChange.OutOfStock, moved);
        Assert.Equal(CartChange.OutOfStock, unknown);
        Assert.Equal(locations[0], line.LocationId);
    }

    [Fact]
    public async Task The_chosen_location_is_honored_and_its_stock_is_decremented()
    {
        var product = (await _fixture.PhysicalProductsAsync(1))[0];
        var locations = await _fixture.LocationIdsAsync();
        await _fixture.SetStockAsync(product.Id, locations[0], 5);
        await _fixture.SetStockAsync(product.Id, locations[1], 5);
        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 2);
        await _fixture.Carts.SetLocationAsync(change.CartId, product.Id, locations[1]);

        var result = await _fixture.Placement.PlaceAsync(Request(change.CartId));

        Assert.True(result.Succeeded);
        var item = (await OrderAsync(result.OrderNumber!)).Items.Single();
        Assert.Equal(locations[1], item.LocationId);
        Assert.Equal(5, await _fixture.StockAsync(product.Id, locations[0]));
        Assert.Equal(3, await _fixture.StockAsync(product.Id, locations[1]));
    }

    [Fact]
    public async Task An_order_may_mix_pickup_locations_one_per_line()
    {
        var products = await _fixture.PhysicalProductsAsync(2);
        var locations = await _fixture.LocationIdsAsync();
        foreach (var product in products)
        {
            await _fixture.SetStockAsync(product.Id, locations[0], 5);
            await _fixture.SetStockAsync(product.Id, locations[1], 5);
        }

        var first = await _fixture.Carts.AddAsync(null, null, products[0].Id, 1);
        await _fixture.Carts.AddAsync(first.CartId, null, products[1].Id, 1);
        await _fixture.Carts.SetLocationAsync(first.CartId, products[1].Id, locations[1]);
        var view = await _fixture.Carts.GetAsync(first.CartId);

        var result = await _fixture.Placement.PlaceAsync(Request(first.CartId));

        Assert.Equal(2, view.PickupGroups.Count);
        Assert.True(result.Succeeded);
        var items = (await OrderAsync(result.OrderNumber!)).Items;
        Assert.Equal(locations[0], items.Single(item => item.ProductId == products[0].Id).LocationId);
        Assert.Equal(locations[1], items.Single(item => item.ProductId == products[1].Id).LocationId);
        var detail = await _fixture.Queries.GetByNumberAsync(result.OrderNumber!);
        Assert.Equal(2, detail!.Lines.Select(line => line.LocationName).Distinct().Count());
        Assert.All(detail.Lines, line => Assert.False(string.IsNullOrEmpty(line.LocationAddress)));
    }

    [Fact]
    public async Task A_line_whose_chosen_location_ran_out_fails_the_order_without_touching_other_lines()
    {
        var products = await _fixture.PhysicalProductsAsync(2);
        var locations = await _fixture.LocationIdsAsync();
        foreach (var product in products)
        {
            await _fixture.SetStockAsync(product.Id, locations[0], 5);
        }

        var cart = await _fixture.Carts.AddAsync(null, null, products[0].Id, 2);
        await _fixture.Carts.AddAsync(cart.CartId, null, products[1].Id, 3);
        await _fixture.SetStockAsync(products[1].Id, locations[0], 1);

        var result = await _fixture.Placement.PlaceAsync(Request(cart.CartId));

        Assert.Equal(PlaceOrderOutcome.OutOfStock, result.Outcome);
        Assert.Contains(products[1].Name, result.Detail, StringComparison.Ordinal);
        _fixture.Db.ChangeTracker.Clear();
        Assert.Equal(5, await _fixture.StockAsync(products[0].Id, locations[0]));
        Assert.Equal(1, await _fixture.StockAsync(products[1].Id, locations[0]));
    }

    [Fact]
    public async Task A_digital_only_cart_skips_pickup()
    {
        var ebook = await _fixture.EbookAsync();
        var change = await _fixture.Carts.AddAsync(null, Buyer, ebook.Id, 1);

        var view = await _fixture.Carts.GetAsync(change.CartId);

        Assert.True(view.IsDigitalOnly);
        Assert.Empty(view.PickupGroups);
        Assert.Single(view.DigitalLines);
        Assert.Null(view.Lines.Single().LocationId);
    }

    [Fact]
    public async Task A_digital_product_needs_a_signed_in_customer()
    {
        var ebook = await _fixture.EbookAsync();
        var refused = await _fixture.Carts.AddAsync(null, null, ebook.Id, 1);
        var change = await _fixture.Carts.AddAsync(null, Buyer, ebook.Id, 1);

        var anonymous = await _fixture.Placement.PlaceAsync(Request(change.CartId));
        var signedIn = await _fixture.Placement.PlaceAsync(Request(change.CartId, Buyer));

        Assert.Equal(CartChange.SignInRequired, refused.Change);
        Assert.Equal(PlaceOrderOutcome.SignInRequired, anonymous.Outcome);
        Assert.True(signedIn.Succeeded);
    }

    [Fact]
    public async Task Order_history_keeps_archived_products_from_the_snapshot_without_a_public_link()
    {
        var product = (await _fixture.PhysicalProductsAsync(1))[0];
        var locations = await _fixture.LocationIdsAsync();
        await _fixture.SetStockAsync(product.Id, locations[0], 5);
        var cart = await _fixture.Carts.AddAsync(null, Buyer, product.Id, 1);
        var placed = await _fixture.Placement.PlaceAsync(Request(cart.CartId, Buyer));
        var before = (await _fixture.Queries.ListForUserAsync(Buyer)).Single().Lines.Single();
        product.Status = CatalogStatus.Archived;
        product.Name = "Renamed after purchase";
        await _fixture.Db.SaveChangesAsync();

        var after = (await _fixture.Queries.ListForUserAsync(Buyer)).Single().Lines.Single();

        Assert.True(placed.Succeeded);
        Assert.NotNull(before.ProductSlug);
        Assert.Null(after.ProductSlug);
        Assert.NotEqual("Renamed after purchase", after.ProductName);
        Assert.Equal(before.ProductName, after.ProductName);
        Assert.Equal(before.ImageUrl, after.ImageUrl);
        Assert.Equal(before.UnitPriceMinor, after.UnitPriceMinor);
    }

    private Task<Order> OrderAsync(string number)
    {
        _fixture.Db.ChangeTracker.Clear();
        return _fixture.Db.Orders.Include(order => order.Items).SingleAsync(order => order.Number == number);
    }

    private static PlaceOrderRequest Request(Guid? cartId, int? userId = null) =>
        new(cartId, userId, "Ana Silva", Phone, null, null, PaymentMethod.MPesa);
}

public sealed class PaymentTests : IDisposable
{
    private const string Phone = "841234567";

    private readonly CheckoutFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Placing_an_order_requests_the_payment_from_paysuite_with_the_method_code_and_checkout_page()
    {
        var number = await PlaceOrderAsync(PaymentMethod.MPesa);
        var order = await LoadOrderAsync(number);

        var request = Assert.Single(_fixture.PaySuite.Requests);
        Assert.Equal("mpesa", request.ProviderCode);
        Assert.Equal(number + "-1", request.Reference);
        Assert.Equal(order.TotalMinor, request.AmountMinor);
        Assert.Equal(PaymentStatus.Pending, order.Payments.Single().Status);
        Assert.Equal("paysuite", order.Payments.Single().Provider);
        Assert.StartsWith("https://paysuite.tech/checkout/", order.Payments.Single().CheckoutUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unavailable_gateway_leaves_a_failed_payment_with_a_reason_like_production()
    {
        _fixture.PaySuite.Unavailable = true;

        var number = await PlaceOrderAsync(PaymentMethod.MPesa);
        var order = await LoadOrderAsync(number);

        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(PaymentStatus.Failed, order.Payments.Single().Status);
        Assert.Equal(PaySuiteClient.UnavailableReason, order.Payments.Single().FailureReason);
    }

    [Fact]
    public async Task Refresh_marks_payment_and_order_paid_and_records_events()
    {
        var number = await PlaceOrderAsync(PaymentMethod.MPesa);

        await _fixture.Payments.RefreshAsync(number);
        var stillPending = await LoadOrderAsync(number);
        _fixture.PaySuite.NextStatus = PaymentStatus.Paid;
        await _fixture.Payments.RefreshAsync(number);
        var paid = await LoadOrderAsync(number);

        Assert.Equal(OrderStatus.AwaitingPayment, stillPending.Status);
        Assert.Equal(OrderStatus.Paid, paid.Status);
        Assert.Equal(PaymentStatus.Paid, paid.Payments.Single().Status);
        Assert.Equal(2, await _fixture.Db.PaymentEvents.CountAsync(paymentEvent => paymentEvent.PaymentId == paid.Payments.Single().Id));
    }

    [Fact]
    public async Task Failed_payment_keeps_the_order_waiting_and_allows_a_retry_that_succeeds()
    {
        var number = await PlaceOrderAsync(PaymentMethod.MPesa);
        _fixture.PaySuite.NextStatus = PaymentStatus.Failed;
        await _fixture.Payments.RefreshAsync(number);

        _fixture.PaySuite.NextStatus = PaymentStatus.Pending;
        var retry = await _fixture.Payments.RetryAsync(number, PaymentMethod.EMola);
        _fixture.PaySuite.NextStatus = PaymentStatus.Paid;
        await _fixture.Payments.RefreshAsync(number);
        var order = await LoadOrderAsync(number);

        Assert.Equal(PaymentRetryOutcome.Started, retry.Outcome);
        Assert.NotNull(retry.CheckoutUrl);
        Assert.Equal(number + "-2", _fixture.PaySuite.Requests[^1].Reference);
        Assert.Equal(2, order.Payments.Count);
        Assert.Contains(order.Payments, payment => payment is { Status: PaymentStatus.Failed, FailureReason: PaySuiteClient.RejectedReason });
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task Card_order_uses_the_credit_card_code()
    {
        var number = await PlaceOrderAsync(PaymentMethod.Card);
        var order = await LoadOrderAsync(number);

        Assert.Equal("credit_card", _fixture.PaySuite.Requests.Single().ProviderCode);
        Assert.Equal(PaymentMethod.Card, order.Payments.Single().Method);
    }

    [Fact]
    public async Task Cash_is_not_accepted_online()
    {
        var product = await _fixture.FirstProductAsync();
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        var result = await _fixture.Placement.PlaceAsync(new PlaceOrderRequest(
            cart.CartId, null, "Ana", Phone, null, null, PaymentMethod.Cash));

        Assert.Equal(PlaceOrderOutcome.InvalidPaymentMethod, result.Outcome);
    }

    [Fact]
    public async Task Inactive_payment_option_is_not_accepted_online()
    {
        var option = await _fixture.Db.PaymentOptions.SingleAsync(candidate => candidate.Method == PaymentMethod.EMola);
        option.IsActive = false;
        await _fixture.Db.SaveChangesAsync();
        var product = await _fixture.FirstProductAsync();
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 1);

        var result = await _fixture.Placement.PlaceAsync(new PlaceOrderRequest(
            cart.CartId, null, "Ana", Phone, null, null, PaymentMethod.EMola));

        Assert.Equal(PlaceOrderOutcome.InvalidPaymentMethod, result.Outcome);
    }

    [Fact]
    public async Task Retry_is_refused_while_a_payment_is_pending()
    {
        var number = await PlaceOrderAsync(PaymentMethod.MPesa);

        var result = await _fixture.Payments.RetryAsync(number, PaymentMethod.MPesa);

        Assert.Equal(PaymentRetryOutcome.NotRetryable, result.Outcome);
    }

    private async Task<string> PlaceOrderAsync(PaymentMethod method)
    {
        var product = await _fixture.FirstProductAsync();
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 1);
        var result = await _fixture.Placement.PlaceAsync(new PlaceOrderRequest(
            cart.CartId, null, "Ana", Phone, null, null, method));
        Assert.True(result.Succeeded);
        return result.OrderNumber!;
    }

    private Task<Order> LoadOrderAsync(string number)
    {
        _fixture.Db.ChangeTracker.Clear();
        return _fixture.Db.Orders.AsNoTracking().Include(order => order.Payments).SingleAsync(order => order.Number == number);
    }
}
