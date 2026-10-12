using Unayka.Core.Payments;

namespace Unayka.Core.Orders;

/// <summary>Customer order, online or at the counter. Amounts are in minor units.</summary>
public sealed class Order
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public OrderChannel Channel { get; set; }
    public FulfillmentKind Fulfillment { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Notes { get; set; }
    public OrderStatus Status { get; set; }
    public long SubtotalMinor { get; set; }
    public long DiscountMinor { get; set; }
    public long DeliveryFeeMinor { get; set; }
    public long TotalMinor { get; set; }
    public string? CouponCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}
