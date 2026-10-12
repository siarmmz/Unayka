namespace Unayka.Core.Orders;

public interface IOrderPlacement
{
    /// <summary>Validates the checkout, recalculates totals, reserves stock and creates the order with a pending payment.</summary>
    Task<PlaceOrderResult> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);
}
