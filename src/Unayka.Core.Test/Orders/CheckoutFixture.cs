using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Data.Seed;
using Unayka.Core.Orders;
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
    }

    public ManualTimeProvider Time { get; }

    public UnaykaDbContext Db => _database.Context;

    public CartService Carts { get; }

    public PaymentService Payments { get; }

    public OrderPlacement Placement { get; }

    public void Dispose() => _database.Dispose();

    public Task<Product> FirstProductAsync() => Db.Products.OrderBy(product => product.Id).FirstAsync();
}
