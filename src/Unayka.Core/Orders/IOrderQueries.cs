using Unayka.Core.Payments;

namespace Unayka.Core.Orders;

/// <summary>Line of an order as shown to the customer.</summary>
public sealed record OrderLineDetail(string ProductName, long UnitPriceMinor, int Quantity)
{
    public long LineTotalMinor => UnitPriceMinor * Quantity;
}

/// <summary>Order summary row for the customer account list.</summary>
public sealed record OrderListItem(string Number, DateTimeOffset CreatedAt, OrderStatus Status, long TotalMinor, int ItemCount);

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
    /// <summary>Lists the orders of a customer, newest first.</summary>
    Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Loads one order by number, or null when it does not exist.</summary>
    Task<OrderDetail?> GetByNumberAsync(string number, CancellationToken cancellationToken = default);
}
