using Unayka.Core.Carts;
using Unayka.Core.Coupons;
using Unayka.Core.Test.Orders;

namespace Unayka.Core.Test.Carts;

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
        product.StockQuantity = 3;
        await _fixture.Db.SaveChangesAsync();

        var change = await _fixture.Carts.AddAsync(null, null, product.Id, 10);
        var view = await _fixture.Carts.GetAsync(change.CartId);

        Assert.Equal(CartChange.LimitedByStock, change.Change);
        Assert.Equal(3, view.ItemCount);
    }

    [Fact]
    public async Task Out_of_stock_product_is_not_added()
    {
        var product = await _fixture.FirstProductAsync();
        product.StockQuantity = 0;
        await _fixture.Db.SaveChangesAsync();

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
