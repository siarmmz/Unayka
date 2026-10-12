namespace Unayka.Core.Orders;

public enum OrderChannel
{
    Online,
    Counter,
}

public enum FulfillmentKind
{
    Pickup,
    Delivery,
}

public enum OrderStatus
{
    New,
    AwaitingPayment,
    Paid,
    Preparing,
    Ready,
    Delivered,
    Cancelled,
}
