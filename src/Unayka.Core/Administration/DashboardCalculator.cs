using Unayka.Core.Orders;

namespace Unayka.Core.Administration;

/// <summary>Minimal order data needed to compute dashboard figures.</summary>
public sealed record OrderSample(DateTimeOffset CreatedAt, OrderStatus Status, long TotalMinor);

/// <summary>Sales total of one local day.</summary>
public sealed record DailySales(DateOnly Day, long TotalMinor);

/// <summary>Headline dashboard figures.</summary>
public sealed record DashboardFigures(
    long TodaySalesMinor,
    long MonthSalesMinor,
    int PendingOrders,
    long AverageTicketMinor,
    IReadOnlyList<DailySales> Daily);

public static class DashboardCalculator
{
    public const int MonthDays = 30;
    public const int ChartDays = 14;

    public static DashboardFigures Compute(IEnumerable<OrderSample> orders, DateTimeOffset now)
    {
        var samples = orders.ToList();
        var today = LocalDays.ToLocalDay(now);
        var monthStart = today.AddDays(1 - MonthDays);
        var paid = samples.Where(sample => AdminOrderGroups.Revenue.Contains(sample.Status)).ToList();
        var paidThisMonth = paid.Where(sample => LocalDays.ToLocalDay(sample.CreatedAt) >= monthStart).ToList();
        var monthTotal = paidThisMonth.Sum(sample => sample.TotalMinor);

        return new DashboardFigures(
            paid.Where(sample => LocalDays.ToLocalDay(sample.CreatedAt) == today).Sum(sample => sample.TotalMinor),
            monthTotal,
            samples.Count(sample => AdminOrderGroups.Open.Contains(sample.Status)),
            paidThisMonth.Count == 0 ? 0 : monthTotal / paidThisMonth.Count,
            BuildDaily(paid, today));
    }

    private static List<DailySales> BuildDaily(List<OrderSample> paid, DateOnly today)
    {
        var totalsByDay = paid
            .GroupBy(sample => LocalDays.ToLocalDay(sample.CreatedAt))
            .ToDictionary(group => group.Key, group => group.Sum(sample => sample.TotalMinor));

        return Enumerable.Range(0, ChartDays)
            .Select(offset => today.AddDays(offset + 1 - ChartDays))
            .Select(day => new DailySales(day, totalsByDay.GetValueOrDefault(day)))
            .ToList();
    }
}
