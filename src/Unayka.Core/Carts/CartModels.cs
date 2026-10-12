using Unayka.Core.Coupons;

namespace Unayka.Core.Carts;

/// <summary>Pickup location that holds a product, with the quantity available there.</summary>
public sealed record CartLocationOption(int LocationId, string Name, int Quantity);

/// <summary>Cart line with the current product data. The unit price always comes from the product. Physical lines carry the pickup location they will be collected at.</summary>
public sealed record CartLineView(
    int ProductId,
    string Slug,
    string Name,
    string ImageUrl,
    int CategoryId,
    long UnitPriceMinor,
    int Quantity,
    int StockQuantity,
    bool IsAvailable,
    bool IsDigital,
    int? LocationId,
    string? LocationName,
    IReadOnlyList<CartLocationOption> LocationOptions)
{
    public long LineTotalMinor => UnitPriceMinor * Quantity;

    public bool ExceedsStock => !IsAvailable || Quantity > StockQuantity;
}

/// <summary>Physical cart lines collected at the same location.</summary>
public sealed record CartPickupGroup(int? LocationId, string LocationName, IReadOnlyList<CartLineView> Lines);

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

    public bool HasDigital => Lines.Any(line => line.IsDigital);

    public bool IsDigitalOnly => HasDigital && Lines.All(line => line.IsDigital);

    public IReadOnlyList<CartLineView> DigitalLines => Lines.Where(line => line.IsDigital).ToList();

    public IReadOnlyList<CartPickupGroup> PickupGroups =>
        Lines.Where(line => !line.IsDigital)
            .GroupBy(line => line.LocationId)
            .Select(group => new CartPickupGroup(group.Key, group.First().LocationName ?? string.Empty, group.ToList()))
            .OrderBy(group => group.LocationName, StringComparer.Ordinal)
            .ToList();
}

public enum CartChange
{
    Added,
    LimitedByStock,
    OutOfStock,
    UnknownProduct,
    SignInRequired,
    LocationChanged,
}

public sealed record CartChangeResult(Guid CartId, CartChange Change);

public enum CouponApplyOutcome
{
    Applied,
    Unknown,
    Rejected,
}

public sealed record CouponApplyResult(CouponApplyOutcome Outcome, CouponRejection? Rejection);
