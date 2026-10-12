namespace Unayka.Core.Coupons;

public enum CouponKind
{
    Percentage,
    Fixed,
}

/// <summary>Discount code. Value is basis points for Percentage (1000 is 10 percent) and minor units for Fixed.</summary>
public sealed class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CouponKind Kind { get; set; }
    public long Value { get; set; }
    public long MinSubtotalMinor { get; set; }
    public int? CategoryId { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
