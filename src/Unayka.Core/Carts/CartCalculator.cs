using Unayka.Core.Coupons;

namespace Unayka.Core.Carts;

public static class CartCalculator
{
    public static CartView Build(IReadOnlyList<CartLineView> lines, Coupon? coupon, DateTimeOffset now)
    {
        var subtotal = lines.Sum(line => line.LineTotalMinor);
        if (coupon is null)
        {
            return new CartView(lines, subtotal, 0, null, null);
        }

        var evaluation = CouponEvaluator.Evaluate(
            coupon, lines.Select(line => new CouponLine(line.CategoryId, line.LineTotalMinor)).ToList(), now);
        return new CartView(lines, subtotal, evaluation.DiscountMinor, coupon.Code, evaluation.Rejection);
    }
}
