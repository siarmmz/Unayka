namespace Unayka.Core.Coupons;

public enum CouponRejection
{
    Inactive,
    NotStarted,
    Expired,
    UsageLimitReached,
    MinSubtotalNotReached,
    CategoryNotInCart,
}

/// <summary>Cart line reduced to what a coupon needs: its category and its total.</summary>
public sealed record CouponLine(int CategoryId, long LineTotalMinor);

/// <summary>Result of evaluating a coupon: a discount, or the reason it was rejected.</summary>
public sealed record CouponEvaluation(long DiscountMinor, CouponRejection? Rejection)
{
    public bool IsAccepted => Rejection is null;

    public static CouponEvaluation Rejected(CouponRejection reason) => new(0, reason);
}

public static class CouponEvaluator
{
    private const long BasisPointsBase = 10_000;

    public static CouponEvaluation Evaluate(Coupon coupon, IReadOnlyList<CouponLine> lines, DateTimeOffset now)
    {
        var rejection = FindRejection(coupon, lines, now);
        if (rejection is not null)
        {
            return CouponEvaluation.Rejected(rejection.Value);
        }

        var eligible = EligibleSubtotal(coupon, lines);
        return new CouponEvaluation(Math.Min(eligible, RawDiscount(coupon, eligible)), null);
    }

    private static CouponRejection? FindRejection(Coupon coupon, IReadOnlyList<CouponLine> lines, DateTimeOffset now)
    {
        if (!coupon.IsActive)
        {
            return CouponRejection.Inactive;
        }

        if (coupon.StartsAt is not null && now < coupon.StartsAt)
        {
            return CouponRejection.NotStarted;
        }

        if (coupon.EndsAt is not null && now > coupon.EndsAt)
        {
            return CouponRejection.Expired;
        }

        if (coupon.UsageLimit is not null && coupon.UsedCount >= coupon.UsageLimit)
        {
            return CouponRejection.UsageLimitReached;
        }

        if (lines.Sum(line => line.LineTotalMinor) < coupon.MinSubtotalMinor)
        {
            return CouponRejection.MinSubtotalNotReached;
        }

        return EligibleSubtotal(coupon, lines) <= 0 ? CouponRejection.CategoryNotInCart : null;
    }

    private static long EligibleSubtotal(Coupon coupon, IReadOnlyList<CouponLine> lines) =>
        lines
            .Where(line => coupon.CategoryId is null || line.CategoryId == coupon.CategoryId)
            .Sum(line => line.LineTotalMinor);

    private static long RawDiscount(Coupon coupon, long eligible) => coupon.Kind switch
    {
        CouponKind.Percentage => eligible * coupon.Value / BasisPointsBase,
        _ => coupon.Value,
    };
}
