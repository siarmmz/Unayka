using Unayka.Core.Counter;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Test.Counter;

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
    [InlineData("841234567", true)]
    [InlineData("841234560", false)]
    [InlineData(null, false)]
    public void Wallet_approves_unless_the_phone_ends_in_zero(string? phone, bool approved)
    {
        Assert.Equal(approved, CounterWallet.Approves(phone));
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
