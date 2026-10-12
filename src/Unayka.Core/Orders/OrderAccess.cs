namespace Unayka.Core.Orders;

public static class OrderAccess
{
    /// <summary>An order is visible to staff, to its owner, and to the browser that placed it.</summary>
    public static bool CanView(OrderDetail order, int? viewerUserId, bool isStaff, IReadOnlyCollection<string> placedNumbers) =>
        isStaff
        || (viewerUserId is not null && order.UserId == viewerUserId)
        || placedNumbers.Contains(order.Number, StringComparer.Ordinal);
}
