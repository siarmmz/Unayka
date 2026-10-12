using Unayka.Core.Payments;

namespace Unayka.Core.Orders;

/// <summary>Checkout form data for placing an online order. Every order is collected at the pickup locations chosen in the cart.</summary>
public sealed record PlaceOrderRequest(
    Guid? CartId,
    int? UserId,
    string? Name,
    string? Phone,
    string? Email,
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
    InvalidWalletPhone,
    InvalidPaymentMethod,
    OutOfStock,
    CouponNoLongerValid,
    SignInRequired,
}

public sealed record PlaceOrderResult(PlaceOrderOutcome Outcome, string? OrderNumber, string? Detail)
{
    public bool Succeeded => Outcome == PlaceOrderOutcome.Placed;
}
