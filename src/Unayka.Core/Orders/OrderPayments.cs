namespace Unayka.Core.Orders;

public static class OrderPayments
{
    /// <summary>True once the payment is confirmed and the order has not been cancelled.</summary>
    public static bool IsPaid(OrderStatus status) =>
        status is OrderStatus.Paid or OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Delivered;

    /// <summary>A digital line can be downloaded by the buyer of a paid order.</summary>
    public static bool CanDownload(OrderDetail order, OrderLineDetail line, int? viewerUserId) =>
        line.IsDigital && viewerUserId is not null && order.UserId == viewerUserId && IsPaid(order.Status);

    /// <summary>Same rule for an account list row.</summary>
    public static bool CanDownload(OrderListItem order, OrderLineDetail line) =>
        line.IsDigital && IsPaid(order.Status);
}
