using Microsoft.EntityFrameworkCore;
using Unayka.Core.Catalog;
using Unayka.Core.Data;

namespace Unayka.Core.Orders;

public sealed class OrderQueries(UnaykaDbContext database) : IOrderQueries
{
    public async Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var orders = await database.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
        var lines = await LoadLinesAsync(orders.SelectMany(order => order.Items).ToList(), cancellationToken);
        return orders
            .Select(order => new OrderListItem(
                order.Number,
                order.CreatedAt,
                order.Status,
                order.TotalMinor,
                order.Items.Sum(item => item.Quantity),
                order.Items.Select(item => lines[item.Id]).ToList()))
            .ToList();
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

        var lines = await LoadLinesAsync(order.Items, cancellationToken);
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
            order.Items.OrderBy(item => item.Id).Select(item => lines[item.Id]).ToList(),
            payment is null ? null : new OrderPaymentDetail(payment.Method, payment.Phone, payment.Status, payment.FailureReason));
    }

    private async Task<Dictionary<int, OrderLineDetail>> LoadLinesAsync(IReadOnlyList<OrderItem> items, CancellationToken cancellationToken)
    {
        var productIds = items.Select(item => item.ProductId).Distinct().ToList();
        var locationIds = items.Where(item => item.LocationId is not null).Select(item => item.LocationId!.Value).Distinct().ToList();
        var slugs = await database.Products.AsNoTracking()
            .Where(ProductVisibility.IsPublic)
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, product => product.Slug, cancellationToken);
        var locations = await database.Locations.AsNoTracking()
            .Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, cancellationToken);
        return items.ToDictionary(
            item => item.Id,
            item =>
            {
                var location = item.LocationId is { } id && locations.TryGetValue(id, out var found) ? found : null;
                return new OrderLineDetail(
                    item.Id,
                    item.ProductName,
                    item.ImageUrl,
                    item.UnitPriceMinor,
                    item.Quantity,
                    item.Kind,
                    slugs.GetValueOrDefault(item.ProductId),
                    item.LocationId,
                    location?.Name,
                    location?.Address,
                    location?.OpeningHours);
            });
    }
}
