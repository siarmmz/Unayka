using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Counter;
using Unayka.Core.Data;
using Unayka.Core.Data.Seed;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Counter;

public sealed class CounterServicesTests : IDisposable
{
    private const int CashierId = 1;
    private const string WalletPhone = "841234567";

    private readonly TestDatabase _database = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
    private readonly CounterShifts _shifts;
    private readonly CounterSales _sales;
    private readonly CounterPreparation _preparation;

    public CounterServicesTests()
    {
        var factory = new TestFactory(_database);
        var audit = new AuditLog(_database.Context, _time);
        _shifts = new CounterShifts(factory, audit, _time);
        _sales = new CounterSales(factory, new OrderNumberGenerator(_database.Context), audit, _time);
        _preparation = new CounterPreparation(factory, audit, _time);
        DatabaseSeeder.SeedAsync(_database.Context, new SeedOptions("unayka2026", "admin@unayka.co.mz", true), _time).GetAwaiter().GetResult();
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Sale_without_open_shift_is_rejected()
    {
        var result = await _sales.RegisterAsync(await CashRequestAsync(1, 100_000));

        Assert.Equal(CounterSaleError.NoOpenShift, result.Error);
    }

    [Fact]
    public async Task Cash_sale_of_a_coffee_creates_a_preparing_order_with_change_and_stock_movement()
    {
        await _shifts.OpenAsync(CashierId, 50_000);
        var product = await FirstProductAsync(CategoryKind.Coffee);
        var request = new CounterSaleRequest(CashierId, [new TicketLine(product.Id, "x", 1, 2)], PaymentMethod.Cash, string.Empty, string.Empty, null, product.PriceMinor * 2 + 5_000);

        var result = await _sales.RegisterAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(5_000, result.Receipt!.ChangeMinor);
        var order = await _database.Context.Orders.Include(o => o.Items).Include(o => o.Payments).SingleAsync(o => o.Number == result.Receipt.Number);
        Assert.Equal(OrderStatus.Preparing, order.Status);
        Assert.Equal(OrderChannel.Counter, order.Channel);
        Assert.Equal(PaymentStatus.Paid, order.Payments.Single().Status);
        Assert.Equal(product.PriceMinor * 2, order.TotalMinor);
        var stock = await _database.Create().Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync();
        Assert.Equal(product.StockQuantity - 2, stock);
        Assert.True(await _database.Context.AuditEntries.AnyAsync(entry => entry.Action == "order.counter"));
    }

    [Fact]
    public async Task Cosmetic_only_ticket_is_delivered_immediately_and_numbers_continue_the_sequence()
    {
        await _shifts.OpenAsync(CashierId, 0);
        var product = await FirstProductAsync(CategoryKind.Cosmetic);
        var request = new CounterSaleRequest(CashierId, [new TicketLine(product.Id, "x", 1, 1)], PaymentMethod.Card, "Ana", string.Empty, null, 0);

        var result = await _sales.RegisterAsync(request);

        Assert.Equal(OrderStatus.Delivered, result.Receipt!.Status);
        Assert.Equal("UN-000021", result.Receipt.Number);
    }

    [Fact]
    public async Task Cash_below_total_and_invalid_wallet_phone_are_rejected()
    {
        await _shifts.OpenAsync(CashierId, 0);
        var product = await FirstProductAsync(CategoryKind.Coffee);
        var line = new TicketLine(product.Id, "x", 1, 1);

        var cash = await _sales.RegisterAsync(new CounterSaleRequest(CashierId, [line], PaymentMethod.Cash, string.Empty, string.Empty, null, 1));
        var wallet = await _sales.RegisterAsync(new CounterSaleRequest(CashierId, [line], PaymentMethod.MPesa, string.Empty, string.Empty, "123", 0));

        Assert.Equal(CounterSaleError.InsufficientCash, cash.Error);
        Assert.Equal(CounterSaleError.InvalidWalletPhone, wallet.Error);
    }

    [Fact]
    public async Task Closing_the_shift_records_expected_cash_from_cash_sales()
    {
        await _shifts.OpenAsync(CashierId, 20_000);
        var free = new TicketLine(TicketLine.FreeAmountProductId, "Servico", 10_000, 1);
        await _sales.RegisterAsync(new CounterSaleRequest(CashierId, [free], PaymentMethod.Cash, string.Empty, string.Empty, null, 10_000));
        await _sales.RegisterAsync(new CounterSaleRequest(CashierId, [free], PaymentMethod.Card, string.Empty, string.Empty, null, 0));

        var summary = await _shifts.CloseAsync(CashierId, 29_000, null);

        Assert.Equal(30_000, summary!.Totals.ExpectedCashMinor);
        Assert.Null(await _shifts.GetOpenAsync());
        var history = await _shifts.GetHistoryAsync(5);
        Assert.Equal(29_000, history.Single().CountedMinor);
        Assert.Equal(-1_000, CashMath.Difference(29_000, history.Single().ExpectedMinor));
    }

    [Fact]
    public async Task Board_lists_orders_and_advance_moves_them_with_an_audit_entry()
    {
        var order = await _database.Context.Orders.FirstAsync();
        order.Status = OrderStatus.Paid;
        order.UpdatedAt = _time.GetUtcNow();
        await _database.Context.SaveChangesAsync();

        var advanced = await _preparation.AdvanceAsync(order.Id, CashierId);
        var board = await _preparation.LoadBoardAsync();

        Assert.True(advanced);
        Assert.Contains(board.Preparing, card => card.OrderId == order.Id);
        Assert.True(await _database.Context.AuditEntries.AnyAsync(entry => entry.Action == "order.status"));
    }

    private async Task<CounterSaleRequest> CashRequestAsync(int quantity, long received)
    {
        var product = await FirstProductAsync(CategoryKind.Coffee);
        return new CounterSaleRequest(CashierId, [new TicketLine(product.Id, "x", 1, quantity)], PaymentMethod.Cash, string.Empty, string.Empty, null, received);
    }

    private Task<Product> FirstProductAsync(CategoryKind kind) =>
        _database.Context.Products.Include(p => p.Category).OrderBy(p => p.Id).FirstAsync(p => p.Category!.Kind == kind && p.StockQuantity > 5);

    private sealed class TestFactory(TestDatabase database) : IDbContextFactory<UnaykaDbContext>
    {
        public UnaykaDbContext CreateDbContext() => database.Create();
    }
}
