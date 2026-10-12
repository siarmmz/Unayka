using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Orders;

public sealed class OrderQueries(UnaykaDbContext database) : IOrderQueries
{
    public async Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await database.Orders.AsNoTracking()
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new
            {
                order.Number,
                order.CreatedAt,
                order.Status,
                order.TotalMinor,
                ItemCount = order.Items.Sum(item => item.Quantity),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new OrderListItem(row.Number, row.CreatedAt, row.Status, row.TotalMinor, row.ItemCount)).ToList();
    }

    public async Task<OrderDetail?> GetByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        var order = await database.Orders.AsNoTracking()
            .Include(candidate => candidate.Items)
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Number == number, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var payment = order.Payments.OrderByDescending(candidate => candidate.Id).FirstOrDefault();
        return new OrderDetail(
            order.Number,
            order.UserId,
            order.Status,
            order.Fulfillment,
            order.CustomerName,
            order.CustomerPhone,
            order.DeliveryAddress,
            order.Notes,
            order.SubtotalMinor,
            order.DiscountMinor,
            order.DeliveryFeeMinor,
            order.TotalMinor,
            order.CouponCode,
            order.CreatedAt,
            order.Items.Select(item => new OrderLineDetail(item.ProductName, item.UnitPriceMinor, item.Quantity)).ToList(),
            payment is null ? null : new OrderPaymentDetail(payment.Method, payment.Phone, payment.Status, payment.FailureReason));
    }
}
