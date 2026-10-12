using Unayka.Core.Catalog;
using Unayka.Core.Payments;

namespace Unayka.Core.Orders;

/// <summary>Line of an order as shown to the customer, read from the snapshot taken at purchase time. ProductSlug is null when the product is no longer publicly visible.</summary>
public sealed record OrderLineDetail(
    int ItemId,
    string ProductName,
    string ImageUrl,
    long UnitPriceMinor,
    int Quantity,
    ProductKind Kind,
    string? ProductSlug,
    int? LocationId,
    string? LocationName,
    string? LocationAddress,
    string? LocationHours)
{
    public long LineTotalMinor => UnitPriceMinor * Quantity;

    public bool IsDigital => Kind == ProductKind.Digital;
}

/// <summary>Order summary row for the customer account list, with its lines.</summary>
public sealed record OrderListItem(
    string Number,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    long TotalMinor,
    int ItemCount,
    IReadOnlyList<OrderLineDetail> Lines);

/// <summary>Latest payment attempt of an order.</summary>
public sealed record OrderPaymentDetail(PaymentMethod Method, string? Phone, PaymentStatus Status, string? FailureReason);

/// <summary>Everything the customer sees about one order.</summary>
public sealed record OrderDetail(
    string Number,
    int? UserId,
    OrderStatus Status,
    FulfillmentKind Fulfillment,
    string CustomerName,
    string CustomerPhone,
    string? DeliveryAddress,
    string? Notes,
    long SubtotalMinor,
    long DiscountMinor,
    long DeliveryFeeMinor,
    long TotalMinor,
    string? CouponCode,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineDetail> Lines,
    OrderPaymentDetail? Payment);

public interface IOrderQueries
{
    /// <summary>Lists the orders of a customer, newest first, with their lines.</summary>
    Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Loads one order by number, or null when it does not exist.</summary>
    Task<OrderDetail?> GetByNumberAsync(string number, CancellationToken cancellationToken = default);
}
