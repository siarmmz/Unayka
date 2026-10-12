using Unayka.Core.Payments;

namespace Unayka.Core.Counter;

/// <summary>Total of paid counter sales for one payment method.</summary>
public sealed record MethodTotal(PaymentMethod Method, int Count, long TotalMinor);

/// <summary>Sales figures of a cash shift derived from its paid payments.</summary>
public sealed record ShiftTotals(
    IReadOnlyList<MethodTotal> ByMethod,
    int SalesCount,
    long SalesMinor,
    long CashSalesMinor,
    long ExpectedCashMinor);

public static class CashMath
{
    private static readonly PaymentMethod[] AllMethods = Enum.GetValues<PaymentMethod>();

    public static long ExpectedCash(long openingMinor, long cashSalesMinor) => openingMinor + cashSalesMinor;

    public static long Difference(long countedMinor, long expectedMinor) => countedMinor - expectedMinor;

    public static bool IsSufficient(long receivedMinor, long totalMinor) => receivedMinor >= totalMinor;

    public static long ChangeDue(long receivedMinor, long totalMinor) => Math.Max(0, receivedMinor - totalMinor);

    public static ShiftTotals Summarize(long openingMinor, IEnumerable<(PaymentMethod Method, long AmountMinor)> payments)
    {
        var list = payments.ToList();
        var byMethod = AllMethods
            .Select(method => new MethodTotal(
                method,
                list.Count(payment => payment.Method == method),
                list.Where(payment => payment.Method == method).Sum(payment => payment.AmountMinor)))
            .ToList();
        var cashSales = byMethod.Single(total => total.Method == PaymentMethod.Cash).TotalMinor;
        return new ShiftTotals(byMethod, list.Count, byMethod.Sum(total => total.TotalMinor), cashSales, ExpectedCash(openingMinor, cashSales));
    }
}
