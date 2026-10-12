using Unayka.Core.Coupons;

namespace Unayka.Core.Carts;

/// <summary>Cart line with the current product data. The unit price always comes from the product.</summary>
public sealed record CartLineView(
    int ProductId,
    string Slug,
    string Name,
    string ImageUrl,
    int CategoryId,
    long UnitPriceMinor,
    int Quantity,
    int StockQuantity,
    bool IsAvailable)
{
    public long LineTotalMinor => UnitPriceMinor * Quantity;

    public bool ExceedsStock => !IsAvailable || Quantity > StockQuantity;
}

/// <summary>Cart totals recalculated on the server from current prices and the applied coupon.</summary>
public sealed record CartView(
    IReadOnlyList<CartLineView> Lines,
    long SubtotalMinor,
    long DiscountMinor,
    string? CouponCode,
    CouponRejection? CouponRejection)
{
    public static readonly CartView Empty = new([], 0, 0, null, null);

    public long TotalMinor => SubtotalMinor - DiscountMinor;

    public int ItemCount => Lines.Sum(line => line.Quantity);

    public bool IsEmpty => Lines.Count == 0;

    public bool HasStockProblem => Lines.Any(line => line.ExceedsStock);
}

public enum CartChange
{
    Added,
    LimitedByStock,
    OutOfStock,
    UnknownProduct,
}

public sealed record CartChangeResult(Guid CartId, CartChange Change);

public enum CouponApplyOutcome
{
    Applied,
    Unknown,
    Rejected,
}

public sealed record CouponApplyResult(CouponApplyOutcome Outcome, CouponRejection? Rejection);
