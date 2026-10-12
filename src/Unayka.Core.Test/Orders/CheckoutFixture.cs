using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Data.Seed;
using Unayka.Core.Locations;
using Unayka.Core.Orders;
using Unayka.Core.Orders.Downloads;
using Unayka.Core.Payments;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Orders;

public sealed class CheckoutFixture : IDisposable
{
    private readonly TestDatabase _database = new();

    public CheckoutFixture()
    {
        Time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
        DatabaseSeeder.SeedAsync(_database.Context, new SeedOptions(string.Empty, "admin@unayka.co.mz", false), Time).GetAwaiter().GetResult();
        Carts = new CartService(Db, Time);
        Payments = new PaymentService(Db, new MockPaymentGateway(Time), new AuditLog(Db, Time), Time);
        Placement = new OrderPlacement(Db, Carts, new OrderNumberGenerator(Db), Payments, new AuditLog(Db, Time), Time);
        Downloads = new DigitalDownloads(Db, new InMemoryFileStore(), new AuditLog(Db, Time), Time);
        Queries = new OrderQueries(Db);
    }

    public ManualTimeProvider Time { get; }

    public UnaykaDbContext Db => _database.Context;

    public CartService Carts { get; }

    public PaymentService Payments { get; }

    public OrderPlacement Placement { get; }

    public DigitalDownloads Downloads { get; }

    public OrderQueries Queries { get; }

    public void Dispose() => _database.Dispose();

    public Task<Product> FirstProductAsync() => Db.Products.OrderBy(product => product.Id).FirstAsync();

    public Task<List<Product>> PhysicalProductsAsync(int count) =>
        Db.Products.Where(product => product.Kind == ProductKind.Physical).OrderBy(product => product.Id).Take(count).ToListAsync();

    public Task<Product> EbookAsync() => Db.Products.SingleAsync(product => product.Slug == "e-book-receitas-com-cha");

    public async Task<List<int>> LocationIdsAsync() =>
        await Db.Locations.OrderBy(location => location.SortOrder).Select(location => location.Id).ToListAsync();

    public async Task SetStockAsync(int productId, int locationId, int quantity)
    {
        var stock = await Db.LocationStocks.FirstOrDefaultAsync(candidate => candidate.ProductId == productId && candidate.LocationId == locationId);
        if (stock is null)
        {
            Db.LocationStocks.Add(new LocationStock { ProductId = productId, LocationId = locationId, Quantity = quantity });
        }
        else
        {
            stock.Quantity = quantity;
        }

        await Db.SaveChangesAsync();
    }

    public Task<int> StockAsync(int productId, int locationId) =>
        Db.LocationStocks.AsNoTracking().Where(stock => stock.ProductId == productId && stock.LocationId == locationId).Select(stock => stock.Quantity).SingleAsync();

    private sealed class InMemoryFileStore : IDigitalFileStore
    {
        public Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Stream? Open(string storageKey) => new MemoryStream([1, 2, 3]);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
