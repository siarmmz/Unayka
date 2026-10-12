using Unayka.Core.Payments;

namespace Unayka.Core.Orders;

/// <summary>Checkout form data for placing an online order.</summary>
public sealed record PlaceOrderRequest(
    Guid? CartId,
    int? UserId,
    string? Name,
    string? Phone,
    string? Email,
    FulfillmentKind Fulfillment,
    string? Address,
    string? Notes,
    PaymentMethod PaymentMethod,
    string? WalletPhone);

public enum PlaceOrderOutcome
{
    Placed,
    EmptyCart,
    InvalidName,
    InvalidPhone,
    InvalidEmail,
    AddressRequired,
    InvalidWalletPhone,
    InvalidPaymentMethod,
    OutOfStock,
    CouponNoLongerValid,
}

public sealed record PlaceOrderResult(PlaceOrderOutcome Outcome, string? OrderNumber, string? Detail)
{
    public bool Succeeded => Outcome == PlaceOrderOutcome.Placed;
}

public static class OrderPricing
{
    public const long DeliveryFeeMinor = 15_000;

    public static long DeliveryFor(FulfillmentKind fulfillment) =>
        fulfillment == FulfillmentKind.Delivery ? DeliveryFeeMinor : 0;
}
