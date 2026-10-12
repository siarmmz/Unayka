using Unayka.Core.Orders;

namespace Unayka.Core.Administration;

public static class OrderStatusRules
{
    public static OrderStatus? NextStep(OrderStatus status) => status switch
    {
        OrderStatus.Paid => OrderStatus.Preparing,
        OrderStatus.Preparing => OrderStatus.Ready,
        OrderStatus.Ready => OrderStatus.Delivered,
        _ => null,
    };

    public static bool CanCancel(OrderStatus status) =>
        status is OrderStatus.New or OrderStatus.AwaitingPayment or OrderStatus.Paid or OrderStatus.Preparing or OrderStatus.Ready;

    public static bool CanMove(OrderStatus from, OrderStatus to) =>
        to == OrderStatus.Cancelled ? CanCancel(from) : NextStep(from) == to;

    public static IReadOnlyList<OrderStatus> AllowedTargets(OrderStatus status)
    {
        var targets = new List<OrderStatus>();
        if (NextStep(status) is { } next)
        {
            targets.Add(next);
        }

        if (CanCancel(status))
        {
            targets.Add(OrderStatus.Cancelled);
        }

        return targets;
    }
}
