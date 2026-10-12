using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Test;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedEnvironment
{
    public const string Name = "Environment";
}

public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan amount) => _now += amount;
}

public sealed class TestDatabase : IDisposable
{
    private const string DatabaseFileName = "database.sqlite";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase(bool withStoreData = false)
    {
        _connection.Open();
        if (withStoreData)
        {
            using var source = new SqliteConnection($"Data Source={StoreDatabasePath()};Mode=ReadOnly");
            source.Open();
            source.BackupDatabase(_connection);
        }

        Context = Create();
        Context.Database.EnsureCreated();
    }

    public static string StoreDatabasePath()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, DatabaseFileName)))
        {
            folder = folder.Parent;
        }

        return folder is null ? throw new FileNotFoundException(DatabaseFileName) : Path.Combine(folder.FullName, DatabaseFileName);
    }

    public UnaykaDbContext Context { get; }

    public UnaykaDbContext Create() =>
        new(new DbContextOptionsBuilder<UnaykaDbContext>().UseSqlite(_connection).Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

public static class TestStock
{
    public static async Task SetTotalAsync(UnaykaDbContext database, int productId, int quantity)
    {
        var stocks = await database.LocationStocks.Where(stock => stock.ProductId == productId).OrderBy(stock => stock.LocationId).ToListAsync();
        foreach (var stock in stocks)
        {
            stock.Quantity = 0;
        }

        stocks[0].Quantity = quantity;
        await database.SaveChangesAsync();
    }

    public static async Task<int> TotalAsync(UnaykaDbContext database, int productId) =>
        await database.LocationStocks.AsNoTracking().Where(stock => stock.ProductId == productId).SumAsync(stock => stock.Quantity);
}

public sealed class TestContextFactory(TestDatabase database) : IDbContextFactory<UnaykaDbContext>
{
    public UnaykaDbContext CreateDbContext() => database.Create();
}

public sealed class InMemoryDigitalFileStore : IDigitalFileStore
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Files[storageKey] = buffer.ToArray();
    }

    public Stream? Open(string storageKey) => Files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null;

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}

public sealed class CheckoutFixture : IDisposable
{
    private readonly TestDatabase _database = new(withStoreData: true);

    public CheckoutFixture()
    {
        Time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
        Db.Users.AddRange(Enumerable.Range(1, 10).Select(index => new Accounts.User { Name = $"Cliente {index}", Role = Accounts.UserRole.Customer, CreatedAt = Time.GetUtcNow() }));
        Db.SaveChanges();
        Carts = new CartService(Db, Time);
        PaySuite = new FakePaySuite();
        Payments = new PaymentService(Db, [new PaySuitePaymentMethod(PaymentMethod.MPesa, PaySuite), new PaySuitePaymentMethod(PaymentMethod.EMola, PaySuite), new PaySuitePaymentMethod(PaymentMethod.Card, PaySuite)], PaySuite, new AuditLog(Db, Time), Time);
        Placement = new OrderPlacement(Db, Carts, new OrderNumberGenerator(Db), Payments, new AuditLog(Db, Time), Time);
        Downloads = new DigitalDownloads(Db, new InMemoryFileStore(), new AuditLog(Db, Time), Time);
        Queries = new OrderQueries(Db);
    }

    public ManualTimeProvider Time { get; }

    public UnaykaDbContext Db => _database.Context;

    public CartService Carts { get; }

    public PaymentService Payments { get; }

    public FakePaySuite PaySuite { get; }

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

public sealed class FakePaySuite : IPaySuiteClient
{
    public PaymentStatus NextStatus { get; set; } = PaymentStatus.Pending;

    public bool Unavailable { get; set; }

    public List<PaymentRequest> Requests { get; } = [];

    public Task<PaymentRequestResult> CreatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            throw new PaymentGatewayException(PaySuiteClient.UnavailableReason);
        }

        Requests.Add(request);
        return Task.FromResult(new PaymentRequestResult($"ps-{Requests.Count}", $"https://paysuite.tech/checkout/ps-{Requests.Count}"));
    }

    public Task<PaymentStatusResult> GetPaymentAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentStatusResult(NextStatus, NextStatus == PaymentStatus.Failed ? PaySuiteClient.RejectedReason : null));

    public PaymentNotification? ReadWebhook(string body, string? signature) => null;
}
