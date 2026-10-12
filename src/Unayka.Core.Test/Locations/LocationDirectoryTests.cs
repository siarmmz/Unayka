using Microsoft.EntityFrameworkCore;
using Unayka.Core.Catalog;
using Unayka.Core.Data.Seed;
using Unayka.Core.Locations;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Locations;

public sealed class LocationDirectoryTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly LocationDirectory _directory;

    public LocationDirectoryTests()
    {
        DatabaseSeeder.SeedAsync(_database.Context, new SeedOptions(string.Empty, "admin@unayka.co.mz", false), TimeProvider.System).GetAwaiter().GetResult();
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
        var productId = await _database.Context.Products.Where(product => product.Slug == "espresso-da-casa").Select(product => product.Id).SingleAsync();

        var locations = await _directory.ActiveAsync();
        var stock = await _directory.StockForProductAsync(productId);

        Assert.Equal(["vilanculos"], locations.Select(location => location.Slug));
        Assert.Single(stock);
    }

    [Fact]
    public async Task Stock_per_location_shows_products_available_in_only_one_place()
    {
        var onlyOneLocation = await _database.Context.Products
            .Where(product => product.Stocks.Count == 1)
            .Select(product => product.Id)
            .FirstAsync();

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
