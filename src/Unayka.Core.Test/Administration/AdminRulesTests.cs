using Unayka.Core.Administration;
using Unayka.Core.Coupons;
using Unayka.Core.Orders;

namespace Unayka.Core.Test.Administration;

public sealed class AdminRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(OrderStatus.Paid, OrderStatus.Preparing, true)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Ready, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Paid, false)]
    public void Status_transitions_follow_the_workflow(OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, OrderStatusRules.CanMove(from, to));
    }

    [Fact]
    public void Allowed_targets_offer_next_step_and_cancel()
    {
        Assert.Equal([OrderStatus.Preparing, OrderStatus.Cancelled], OrderStatusRules.AllowedTargets(OrderStatus.Paid));
        Assert.Empty(OrderStatusRules.AllowedTargets(OrderStatus.Delivered));
    }

    [Fact]
    public void Dashboard_sums_only_paid_orders_per_period()
    {
        var orders = new[]
        {
            new OrderSample(Now, OrderStatus.Paid, 10000),
            new OrderSample(Now.AddHours(-1), OrderStatus.Delivered, 5000),
            new OrderSample(Now, OrderStatus.Cancelled, 99999),
            new OrderSample(Now, OrderStatus.AwaitingPayment, 7000),
            new OrderSample(Now.AddDays(-5), OrderStatus.Ready, 20000),
            new OrderSample(Now.AddDays(-40), OrderStatus.Paid, 80000),
        };

        var figures = DashboardCalculator.Compute(orders, Now);

        Assert.Equal(15000, figures.TodaySalesMinor);
        Assert.Equal(35000, figures.MonthSalesMinor);
        Assert.Equal(11666, figures.AverageTicketMinor);
        Assert.Equal(4, figures.PendingOrders);
    }

    [Fact]
    public void Dashboard_daily_series_covers_fourteen_days_with_zeros()
    {
        var figures = DashboardCalculator.Compute([new OrderSample(Now.AddDays(-2), OrderStatus.Paid, 3000)], Now);

        Assert.Equal(DashboardCalculator.ChartDays, figures.Daily.Count);
        Assert.Equal(LocalDays.ToLocalDay(Now), figures.Daily[^1].Day);
        Assert.Equal(3000, figures.Daily[^3].TotalMinor);
        Assert.Equal(0, figures.Daily[0].TotalMinor);
    }

    [Fact]
    public void Dashboard_with_no_orders_has_zero_average()
    {
        var figures = DashboardCalculator.Compute([], Now);

        Assert.Equal(0, figures.AverageTicketMinor);
        Assert.Equal(0, figures.MonthSalesMinor);
    }

    [Fact]
    public void Dashboard_groups_by_maputo_day()
    {
        var lateEvening = new DateTimeOffset(2026, 10, 11, 21, 30, 0, TimeSpan.Zero);

        var figures = DashboardCalculator.Compute([new OrderSample(lateEvening, OrderStatus.Paid, 1000)], Now);

        Assert.Equal(0, figures.TodaySalesMinor);
        Assert.Equal(1000, figures.Daily.Single(day => day.Day == LocalDays.ToLocalDay(lateEvening)).TotalMinor);
    }

    [Theory]
    [InlineData("Café Moído Especial", "cafe-moido-especial")]
    [InlineData("  Creme & Óleo  ", "creme-oleo")]
    [InlineData("---", "")]
    public void Slugs_are_built_from_names(string name, string expected)
    {
        Assert.Equal(expected, Slugs.FromName(name));
    }

    [Theory]
    [InlineData("cafe-moido", true)]
    [InlineData("Cafe", false)]
    [InlineData("cafe--moido", false)]
    [InlineData("", false)]
    public void Slug_validation(string slug, bool expected)
    {
        Assert.Equal(expected, Slugs.IsValid(slug));
    }

    [Fact]
    public void Money_input_rounds_to_minor_units()
    {
        Assert.Equal(12550, MoneyInput.ToMinor(125.5m));
        Assert.Equal(1, MoneyInput.ToMinor(0.005m));
        Assert.Equal(125.5m, MoneyInput.ToAmount(12550));
    }

    [Theory]
    [InlineData(0, CustomerSegment.NoPurchases)]
    [InlineData(1, CustomerSegment.New)]
    [InlineData(2, CustomerSegment.Recurring)]
    [InlineData(7, CustomerSegment.Recurring)]
    public void Customer_segment_depends_on_paid_orders(int paidOrders, CustomerSegment expected)
    {
        Assert.Equal(expected, CustomerSegments.For(paidOrders));
    }

    [Fact]
    public void Coupon_values_convert_between_input_and_storage()
    {
        Assert.Equal(1000, CouponValues.ToStoredValue(CouponKind.Percentage, 10m));
        Assert.Equal(5000, CouponValues.ToStoredValue(CouponKind.Fixed, 50m));
        Assert.Equal(10m, CouponValues.ToInputValue(CouponKind.Percentage, 1000));
    }

    [Fact]
    public void Coupon_validation_rejects_bad_input()
    {
        var form = new CouponForm { Code = "BAD CODE", Kind = CouponKind.Percentage, Value = 150 };

        Assert.Equal(2, CouponValues.Validate(form).Count);
    }
}
