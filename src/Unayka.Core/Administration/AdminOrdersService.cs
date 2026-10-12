using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Data;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Core.Administration;

/// <summary>Filters of the order list.</summary>
public sealed record OrderFilter(OrderStatus? Status = null, OrderChannel? Channel = null, string? Search = null, DateOnly? From = null, DateOnly? To = null);

/// <summary>Step of a payment timeline.</summary>
public sealed record PaymentTimelineEntry(string Kind, DateTimeOffset At);

/// <summary>Payment of an order with its timeline.</summary>
public sealed record OrderPaymentDetail(
    int Id,
    string Provider,
    PaymentMethod Method,
    PaymentStatus Status,
    long AmountMinor,
    long FeeMinor,
    string? ProviderReference,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PaymentTimelineEntry> Timeline);

/// <summary>Line of an order.</summary>
public sealed record OrderLineDetail(string ProductName, long UnitPriceMinor, int Quantity);

/// <summary>Full order for the detail page.</summary>
public sealed record OrderDetail(
    int Id,
    string Number,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    OrderChannel Channel,
    FulfillmentKind Fulfillment,
    string? DeliveryAddress,
    string? Notes,
    OrderStatus Status,
    long SubtotalMinor,
    long DiscountMinor,
    long DeliveryFeeMinor,
    long TotalMinor,
    string? CouponCode,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineDetail> Lines,
    IReadOnlyList<OrderPaymentDetail> Payments);

public interface IAdminOrders
{
    /// <summary>Lists orders newest first with filters and paging.</summary>
    Task<PagedResult<OrderSummary>> ListAsync(OrderFilter filter, int page, CancellationToken cancellationToken = default);

    /// <summary>Loads one order by its number, or null when it does not exist.</summary>
    Task<OrderDetail?> GetAsync(string number, CancellationToken cancellationToken = default);

    /// <summary>Moves an order to a new status following the allowed transitions and audits the change.</summary>
    Task<AdminResult> ChangeStatusAsync(int? actorUserId, string number, OrderStatus target, CancellationToken cancellationToken = default);
}

public sealed class AdminOrdersService(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit, TimeProvider time) : IAdminOrders
{
    public async Task<PagedResult<OrderSummary>> ListAsync(OrderFilter filter, int page, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = ApplyFilter(db.Orders.AsNoTracking(), filter);
        var pageNumber = AdminPaging.NormalizePage(page);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(order => order.CreatedAt)
            .Skip((pageNumber - 1) * AdminPaging.DefaultPageSize)
            .Take(AdminPaging.DefaultPageSize)
            .Select(order => new OrderSummary(order.Id, order.Number, order.CustomerName, order.Channel, order.Status, order.TotalMinor, order.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<OrderSummary>(items, pageNumber, AdminPaging.DefaultPageSize, total);
    }

    public async Task<OrderDetail?> GetAsync(string number, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders.AsNoTracking()
            .Include(candidate => candidate.Items)
            .Include(candidate => candidate.Payments).ThenInclude(payment => payment.Events)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Number == number, cancellationToken);
        return order is null ? null : ToDetail(order);
    }

    public async Task<AdminResult> ChangeStatusAsync(int? actorUserId, string number, OrderStatus target, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders.Include(candidate => candidate.Items).FirstOrDefaultAsync(candidate => candidate.Number == number, cancellationToken);
        if (order is null)
        {
            return AdminResult.Fail("Pedido não encontrado.");
        }

        if (!OrderStatusRules.CanMove(order.Status, target))
        {
            return AdminResult.Fail("Esta alteração de estado não é permitida.");
        }

        var previous = order.Status;
        order.Status = target;
        order.UpdatedAt = time.GetUtcNow();
        if (target == OrderStatus.Cancelled)
        {
            await RestoreStockAsync(db, order, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            actorUserId,
            AdminAuditActions.OrderStatusChanged,
            AdminEntityTypes.Order,
            order.Number,
            $"Estado do pedido {order.Number} alterado de {StatusLabels.For(previous).Label} para {StatusLabels.For(target).Label}.",
            cancellationToken);
        return AdminResult.Ok(order.Id);
    }

    private static async Task RestoreStockAsync(UnaykaDbContext db, Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(item => item.ProductId).ToList();
        var products = await db.Products.Where(product => productIds.Contains(product.Id)).ToListAsync(cancellationToken);
        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(candidate => candidate.Id == item.ProductId);
            if (product is not null)
            {
                product.StockQuantity += item.Quantity;
            }
        }
    }

    private static IQueryable<Order> ApplyFilter(IQueryable<Order> query, OrderFilter filter)
    {
        if (filter.Status is { } status)
        {
            query = query.Where(order => order.Status == status);
        }

        if (filter.Channel is { } channel)
        {
            query = query.Where(order => order.Channel == channel);
        }

        if (filter.From is { } from)
        {
            var start = LocalDays.StartOfDay(from);
            query = query.Where(order => order.CreatedAt >= start);
        }

        if (filter.To is { } to)
        {
            var end = LocalDays.StartOfDay(to.AddDays(1));
            query = query.Where(order => order.CreatedAt < end);
        }

        return SearchPattern.Contains(filter.Search) is { } pattern
            ? query.Where(order => EF.Functions.Like(order.Number, pattern) || EF.Functions.Like(order.CustomerName, pattern) || EF.Functions.Like(order.CustomerPhone, pattern))
            : query;
    }

    private static OrderDetail ToDetail(Order order) => new(
        order.Id,
        order.Number,
        order.CustomerName,
        order.CustomerPhone,
        order.CustomerEmail,
        order.Channel,
        order.Fulfillment,
        order.DeliveryAddress,
        order.Notes,
        order.Status,
        order.SubtotalMinor,
        order.DiscountMinor,
        order.DeliveryFeeMinor,
        order.TotalMinor,
        order.CouponCode,
        order.CreatedAt,
        order.Items.Select(item => new OrderLineDetail(item.ProductName, item.UnitPriceMinor, item.Quantity)).ToList(),
        order.Payments
            .OrderBy(payment => payment.CreatedAt)
            .Select(payment => new OrderPaymentDetail(
                payment.Id,
                payment.Provider,
                payment.Method,
                payment.Status,
                payment.AmountMinor,
                payment.FeeMinor,
                payment.ProviderReference,
                payment.FailureReason,
                payment.CreatedAt,
                payment.Events.OrderBy(entry => entry.At).Select(entry => new PaymentTimelineEntry(entry.Kind, entry.At)).ToList()))
            .ToList());
}
