using Unayka.Core.Coupons;

namespace Unayka.Core.Test.Coupons;

public sealed class CouponEvaluatorTests
{
    private const int CoffeeCategory = 1;
    private const int SkinCategory = 2;

    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly CouponLine[] Lines = [new(CoffeeCategory, 20_000), new(SkinCategory, 10_000)];

    [Fact]
    public void Percentage_coupon_discounts_basis_points_of_the_subtotal()
    {
        var result = CouponEvaluator.Evaluate(Percentage(1000), Lines, Now);

        Assert.True(result.IsAccepted);
        Assert.Equal(3_000, result.DiscountMinor);
    }

    [Fact]
    public void Fixed_coupon_discounts_the_fixed_value()
    {
        var result = CouponEvaluator.Evaluate(Fixed(5_000), Lines, Now);

        Assert.Equal(5_000, result.DiscountMinor);
    }

    [Fact]
    public void Fixed_coupon_never_exceeds_the_subtotal()
    {
        var result = CouponEvaluator.Evaluate(Fixed(99_000), Lines, Now);

        Assert.Equal(30_000, result.DiscountMinor);
    }

    [Fact]
    public void Inactive_coupon_is_rejected()
    {
        var coupon = Percentage(1000);
        coupon.IsActive = false;

        Assert.Equal(CouponRejection.Inactive, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_before_its_start_is_rejected()
    {
        var coupon = Percentage(1000);
        coupon.StartsAt = Now.AddDays(1);

        Assert.Equal(CouponRejection.NotStarted, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_after_its_end_is_rejected()
    {
        var coupon = Percentage(1000);
        coupon.EndsAt = Now.AddMinutes(-1);

        Assert.Equal(CouponRejection.Expired, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_inside_its_window_is_accepted()
    {
        var coupon = Percentage(1000);
        coupon.StartsAt = Now.AddDays(-1);
        coupon.EndsAt = Now.AddDays(1);

        Assert.True(CouponEvaluator.Evaluate(coupon, Lines, Now).IsAccepted);
    }

    [Fact]
    public void Coupon_that_reached_its_usage_limit_is_rejected()
    {
        var coupon = Percentage(1000);
        coupon.UsageLimit = 3;
        coupon.UsedCount = 3;

        Assert.Equal(CouponRejection.UsageLimitReached, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_below_its_usage_limit_is_accepted()
    {
        var coupon = Percentage(1000);
        coupon.UsageLimit = 3;
        coupon.UsedCount = 2;

        Assert.True(CouponEvaluator.Evaluate(coupon, Lines, Now).IsAccepted);
    }

    [Fact]
    public void Coupon_with_a_higher_minimum_subtotal_is_rejected()
    {
        var coupon = Fixed(5_000);
        coupon.MinSubtotalMinor = 30_001;

        Assert.Equal(CouponRejection.MinSubtotalNotReached, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Coupon_exactly_at_its_minimum_subtotal_is_accepted()
    {
        var coupon = Fixed(5_000);
        coupon.MinSubtotalMinor = 30_000;

        Assert.True(CouponEvaluator.Evaluate(coupon, Lines, Now).IsAccepted);
    }

    [Fact]
    public void Category_coupon_only_discounts_lines_of_that_category()
    {
        var coupon = Percentage(5000);
        coupon.CategoryId = CoffeeCategory;

        Assert.Equal(10_000, CouponEvaluator.Evaluate(coupon, Lines, Now).DiscountMinor);
    }

    [Fact]
    public void Category_coupon_without_matching_lines_is_rejected()
    {
        var coupon = Percentage(5000);
        coupon.CategoryId = 99;

        Assert.Equal(CouponRejection.CategoryNotInCart, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Fixed_category_coupon_is_capped_by_the_category_subtotal()
    {
        var coupon = Fixed(15_000);
        coupon.CategoryId = SkinCategory;

        Assert.Equal(10_000, CouponEvaluator.Evaluate(coupon, Lines, Now).DiscountMinor);
    }

    [Fact]
    public void Minimum_subtotal_counts_the_whole_cart_even_for_category_coupons()
    {
        var coupon = Fixed(1_000);
        coupon.CategoryId = SkinCategory;
        coupon.MinSubtotalMinor = 25_000;

        Assert.True(CouponEvaluator.Evaluate(coupon, Lines, Now).IsAccepted);
    }

    [Fact]
    public void Inactive_is_reported_before_expired()
    {
        var coupon = Percentage(1000);
        coupon.IsActive = false;
        coupon.EndsAt = Now.AddDays(-1);

        Assert.Equal(CouponRejection.Inactive, CouponEvaluator.Evaluate(coupon, Lines, Now).Rejection);
    }

    [Fact]
    public void Empty_cart_is_rejected_by_the_minimum_or_the_category_rule()
    {
        var result = CouponEvaluator.Evaluate(Percentage(1000), [], Now);

        Assert.Equal(CouponRejection.CategoryNotInCart, result.Rejection);
    }

    private static Coupon Percentage(long basisPoints) =>
        new() { Code = "TEST", Kind = CouponKind.Percentage, Value = basisPoints };

    private static Coupon Fixed(long minor) =>
        new() { Code = "TEST", Kind = CouponKind.Fixed, Value = minor };
}
