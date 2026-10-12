using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Data;
using Unayka.Core.Orders;
using Unayka.Core.Presentation;

namespace Unayka.Core.Counter;

public sealed class CounterPreparation(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit, TimeProvider time) : ICounterPreparation
{
    private const string OrderEntity = "Order";

    private static readonly OrderStatus[] BoardStatuses =
        [OrderStatus.New, OrderStatus.Paid, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Delivered];

    public async Task<PreparationBoard> LoadBoardAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var dayStart = CounterTimes.StartOfDay(now);
        var orders = await database.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => BoardStatuses.Contains(order.Status) && (order.Status != OrderStatus.Delivered || order.UpdatedAt >= dayStart))
            .OrderBy(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
        var cards = orders.Select(order => ToCard(order, now)).ToList();
        return new PreparationBoard(
            cards.Where(card => card.Status is OrderStatus.New or OrderStatus.Paid).ToList(),
            cards.Where(card => card.Status == OrderStatus.Preparing).ToList(),
            cards.Where(card => card.Status == OrderStatus.Ready).ToList(),
            cards.Where(card => card.Status == OrderStatus.Delivered).OrderByDescending(card => card.OrderId).ToList());
    }

    public async Task<bool> AdvanceAsync(int orderId, int userId, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var order = await database.Orders.FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
        var next = order is null ? null : PreparationFlow.Next(order.Status);
        if (order is null || next is null)
        {
            return false;
        }

        var previous = order.Status;
        order.Status = next.Value;
        order.UpdatedAt = time.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(userId, "order.status", OrderEntity, order.Id.ToString(CultureInfo.InvariantCulture), $"{order.Number} {StatusLabels.For(previous).Label} to {StatusLabels.For(next.Value).Label}", cancellationToken);
        return true;
    }

    private static PreparationCard ToCard(Order order, DateTimeOffset now) => new(
        order.Id,
        order.Number,
        order.CustomerName,
        order.Channel,
        order.Status,
        order.Items.Select(item => $"{item.Quantity} x {item.ProductName}").ToList(),
        CounterTimes.AgeInMinutes(order.CreatedAt, now));
}
