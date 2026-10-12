using Microsoft.EntityFrameworkCore;
using Unayka.Core.Catalog;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Orders;

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
    [InlineData("", Phone, Phone, PlaceOrderOutcome.InvalidName)]
    [InlineData("Ana", "123", Phone, PlaceOrderOutcome.InvalidPhone)]
    [InlineData("Ana", Phone, "12", PlaceOrderOutcome.InvalidWalletPhone)]
    public async Task Invalid_input_is_rejected(string name, string phone, string wallet, PlaceOrderOutcome expected)
    {
        var request = new PlaceOrderRequest(
            null, null, name, phone, null, null, PaymentMethod.MPesa, wallet);

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
        new(cartId, userId, "Ana Silva", Phone, null, null, PaymentMethod.MPesa, Phone);
}
