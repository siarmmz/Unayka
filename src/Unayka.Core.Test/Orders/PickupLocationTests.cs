using Microsoft.EntityFrameworkCore;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Test.Orders;

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
        new(cartId, userId, "Ana Silva", Phone, null, null, PaymentMethod.MPesa, Phone);
}
