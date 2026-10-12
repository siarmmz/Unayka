using Unayka.Core.Administration;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Counter;
using Unayka.Core.Data;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Test;

public sealed class CashMathTests
{
    [Fact]
    public void Expected_cash_is_opening_plus_cash_sales()
    {
        Assert.Equal(150_000, CashMath.ExpectedCash(100_000, 50_000));
    }

    [Theory]
    [InlineData(150_000, 150_000, 0)]
    [InlineData(149_000, 150_000, -1_000)]
    [InlineData(151_500, 150_000, 1_500)]
    public void Difference_is_counted_minus_expected(long counted, long expected, long difference)
    {
        Assert.Equal(difference, CashMath.Difference(counted, expected));
    }

    [Theory]
    [InlineData(100_000, 85_000, 15_000)]
    [InlineData(85_000, 85_000, 0)]
    [InlineData(50_000, 85_000, 0)]
    public void Change_due_never_goes_below_zero(long received, long total, long change)
    {
        Assert.Equal(change, CashMath.ChangeDue(received, total));
    }

    [Fact]
    public void Received_cash_is_sufficient_only_when_it_covers_the_total()
    {
        Assert.True(CashMath.IsSufficient(85_000, 85_000));
        Assert.False(CashMath.IsSufficient(84_999, 85_000));
    }

    [Fact]
    public void Summary_groups_payments_by_method_and_counts_only_cash_in_the_drawer()
    {
        var totals = CashMath.Summarize(
            20_000,
            [(PaymentMethod.Cash, 10_000), (PaymentMethod.Cash, 5_000), (PaymentMethod.Card, 30_000), (PaymentMethod.MPesa, 7_000)]);

        Assert.Equal(4, totals.SalesCount);
        Assert.Equal(52_000, totals.SalesMinor);
        Assert.Equal(15_000, totals.CashSalesMinor);
        Assert.Equal(35_000, totals.ExpectedCashMinor);
        Assert.Equal(2, totals.ByMethod.Single(total => total.Method == PaymentMethod.Cash).Count);
        Assert.Equal(0, totals.ByMethod.Single(total => total.Method == PaymentMethod.EMola).TotalMinor);
    }

    [Theory]
    [InlineData("150", 15_000)]
    [InlineData("12,50", 1_250)]
    [InlineData(" 1 000.5 ", 100_050)]
    [InlineData("0", 0)]
    public void Money_input_parses_amounts_in_meticais(string text, long minor)
    {
        Assert.True(MoneyInput.TryParse(text, out var parsed));
        Assert.Equal(minor, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("99999999999")]
    public void Money_input_rejects_invalid_amounts(string text)
    {
        Assert.False(MoneyInput.TryParse(text, out _));
        Assert.Equal(0, MoneyInput.ParseOrZero(text));
    }

    [Fact]
    public void Ticket_total_sums_quantities_times_prices()
    {
        var total = Ticket.Total([new TicketLine(1, "Cafe", 5_000, 3), new TicketLine(TicketLine.FreeAmountProductId, "Servico", 2_500, 1)]);

        Assert.Equal(17_500, total);
    }

    [Theory]
    [InlineData(OrderStatus.New, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Paid, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready)]
    [InlineData(OrderStatus.Ready, OrderStatus.Delivered)]
    public void Preparation_advances_one_step(OrderStatus from, OrderStatus to)
    {
        Assert.Equal(to, PreparationFlow.Next(from));
    }

    [Fact]
    public void Delivered_and_cancelled_orders_do_not_advance()
    {
        Assert.Null(PreparationFlow.Next(OrderStatus.Delivered));
        Assert.Null(PreparationFlow.Next(OrderStatus.Cancelled));
    }
}

public sealed class CounterServicesTests : IDisposable
{
    private const int CashierId = 1;
    private const string WalletPhone = "841234567";

    private readonly TestDatabase _database = new(withStoreData: true);
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
        _database.Context.Users.Add(new Accounts.User { Name = "Caixa", Email = "caixa@unayka.co.mz", Role = Accounts.UserRole.Cashier, CreatedAt = _time.GetUtcNow() });
        _database.Context.SaveChanges();
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
        var product = await FirstProductAsync("chas");
        var stockBefore = await TestStock.TotalAsync(_database.Create(), product.Id);
        var request = new CounterSaleRequest(CashierId, [new TicketLine(product.Id, "x", 1, 2)], PaymentMethod.Cash, string.Empty, string.Empty, null, product.PriceMinor * 2 + 5_000);

        var result = await _sales.RegisterAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(5_000, result.Receipt!.ChangeMinor);
        var order = await _database.Context.Orders.Include(o => o.Items).Include(o => o.Payments).SingleAsync(o => o.Number == result.Receipt.Number);
        Assert.Equal(OrderStatus.Preparing, order.Status);
        Assert.Equal(OrderChannel.Counter, order.Channel);
        Assert.Equal(PaymentStatus.Paid, order.Payments.Single().Status);
        Assert.Equal(product.PriceMinor * 2, order.TotalMinor);
        Assert.Equal(stockBefore - 2, await TestStock.TotalAsync(_database.Create(), product.Id));
        Assert.True(await _database.Context.AuditEntries.AnyAsync(entry => entry.Action == "order.counter"));
    }

    [Fact]
    public async Task Cosmetic_only_ticket_is_delivered_immediately_and_numbers_continue_the_sequence()
    {
        await _shifts.OpenAsync(CashierId, 0);
        var product = await FirstProductAsync("cosmeticos");
        var request = new CounterSaleRequest(CashierId, [new TicketLine(product.Id, "x", 1, 1)], PaymentMethod.Card, "Ana", string.Empty, null, 0);

        var result = await _sales.RegisterAsync(request);

        Assert.Equal(OrderStatus.Delivered, result.Receipt!.Status);
        Assert.Equal("UN-000001", result.Receipt.Number);
    }

    [Fact]
    public async Task Cash_below_total_and_invalid_wallet_phone_are_rejected()
    {
        await _shifts.OpenAsync(CashierId, 0);
        var product = await FirstProductAsync("chas");
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
        await _shifts.OpenAsync(CashierId, 0);
        var sale = await _sales.RegisterAsync(await CashRequestAsync(1, 100_000));
        var order = await _database.Context.Orders.SingleAsync(candidate => candidate.Number == sale.Receipt!.Number);

        var advanced = await _preparation.AdvanceAsync(order.Id, CashierId);
        var board = await _preparation.LoadBoardAsync();

        Assert.True(advanced);
        Assert.Contains(board.Ready, card => card.OrderId == order.Id);
        Assert.True(await _database.Context.AuditEntries.AnyAsync(entry => entry.Action == "order.status"));
    }

    private async Task<CounterSaleRequest> CashRequestAsync(int quantity, long received)
    {
        var product = await FirstProductAsync("chas");
        return new CounterSaleRequest(CashierId, [new TicketLine(product.Id, "x", 1, quantity)], PaymentMethod.Cash, string.Empty, string.Empty, null, received);
    }

    private Task<Product> FirstProductAsync(string sectionSlug) =>
        _database.Context.Products.OrderBy(p => p.Id).FirstAsync(p => p.Category!.Section!.Slug == sectionSlug && p.Stocks.Sum(stock => stock.Quantity) > 5);

    private sealed class TestFactory(TestDatabase database) : IDbContextFactory<UnaykaDbContext>
    {
        public UnaykaDbContext CreateDbContext() => database.Create();
    }
}
