using Unayka.Core.Orders;

namespace Unayka.Core.Counter;

public static class PreparationFlow
{
    public static OrderStatus? Next(OrderStatus status) => status switch
    {
        OrderStatus.New or OrderStatus.Paid => OrderStatus.Preparing,
        OrderStatus.Preparing => OrderStatus.Ready,
        OrderStatus.Ready => OrderStatus.Delivered,
        _ => null,
    };

    public static OrderStatus InitialStatus(bool needsPreparation) => needsPreparation ? OrderStatus.Preparing : OrderStatus.Delivered;
}
