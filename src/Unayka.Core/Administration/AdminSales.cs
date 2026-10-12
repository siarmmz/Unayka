using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Coupons;
using Unayka.Core.Data;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

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

/// <summary>Filters of the order list.</summary>
public sealed record OrderFilter(OrderStatus? Status = null, OrderChannel? Channel = null, string? Search = null, DateOnly? From = null, DateOnly? To = null);

/// <summary>Step of a payment timeline.</summary>
public sealed record PaymentTimelineEntry(string Kind, DateTimeOffset At);

/// <summary>Payment of an order with its timeline.</summary>
public sealed record AdminOrderPaymentDetail(
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
public sealed record AdminOrderLineDetail(string ProductName, long UnitPriceMinor, int Quantity);

/// <summary>Full order for the detail page.</summary>
public sealed record AdminOrderDetail(
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
    IReadOnlyList<AdminOrderLineDetail> Lines,
    IReadOnlyList<AdminOrderPaymentDetail> Payments);

public interface IAdminOrders
{
    /// <summary>Lists orders newest first with filters and paging.</summary>
    Task<PagedResult<OrderSummary>> ListAsync(OrderFilter filter, int page, CancellationToken cancellationToken = default);

    /// <summary>Loads one order by its number, or null when it does not exist.</summary>
    Task<AdminOrderDetail?> GetAsync(string number, CancellationToken cancellationToken = default);

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

    public async Task<AdminOrderDetail?> GetAsync(string number, CancellationToken cancellationToken = default)
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
        var stocks = await db.LocationStocks.Where(stock => productIds.Contains(stock.ProductId)).ToListAsync(cancellationToken);
        foreach (var item in order.Items.Where(item => item.LocationId is not null))
        {
            var stock = stocks.FirstOrDefault(candidate => candidate.ProductId == item.ProductId && candidate.LocationId == item.LocationId);
            if (stock is not null)
            {
                stock.Quantity += item.Quantity;
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

    private static AdminOrderDetail ToDetail(Order order) => new(
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
        order.Items.Select(item => new AdminOrderLineDetail(item.ProductName, item.UnitPriceMinor, item.Quantity)).ToList(),
        order.Payments
            .OrderBy(payment => payment.CreatedAt)
            .Select(payment => new AdminOrderPaymentDetail(
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

/// <summary>Filters of the payment list.</summary>
public sealed record PaymentFilter(PaymentStatus? Status = null, PaymentMethod? Method = null, string? Provider = null, DateOnly? From = null, DateOnly? To = null);

/// <summary>Row of the payment list.</summary>
public sealed record PaymentRow(int Id, int OrderId, string OrderNumber, string Provider, PaymentMethod Method, PaymentStatus Status, long AmountMinor, long FeeMinor, DateTimeOffset CreatedAt);

/// <summary>Count and amount of payments with one status.</summary>
public sealed record PaymentStatusTotal(PaymentStatus Status, int Count, long AmountMinor);

/// <summary>Payments page with totals per status.</summary>
public sealed record PaymentsOverview(PagedResult<PaymentRow> Page, IReadOnlyList<PaymentStatusTotal> Totals, IReadOnlyList<string> Providers);

public interface IAdminPayments
{
    /// <summary>Lists payments with filters, paging and totals per status.</summary>
    Task<PaymentsOverview> ListAsync(PaymentFilter filter, int page, CancellationToken cancellationToken = default);
}

public sealed class AdminPaymentsService(IDbContextFactory<UnaykaDbContext> factory) : IAdminPayments
{
    public async Task<PaymentsOverview> ListAsync(PaymentFilter filter, int page, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var scoped = ApplyScope(db.Payments.AsNoTracking(), filter);
        var filtered = filter.Status is { } status ? scoped.Where(payment => payment.Status == status) : scoped;
        var pageNumber = AdminPaging.NormalizePage(page);
        var total = await filtered.CountAsync(cancellationToken);
        var rows = await filtered
            .OrderByDescending(payment => payment.CreatedAt)
            .Skip((pageNumber - 1) * AdminPaging.DefaultPageSize)
            .Take(AdminPaging.DefaultPageSize)
            .Select(payment => new PaymentRow(payment.Id, payment.OrderId, payment.Order!.Number, payment.Provider, payment.Method, payment.Status, payment.AmountMinor, payment.FeeMinor, payment.CreatedAt))
            .ToListAsync(cancellationToken);
        var totals = await scoped
            .GroupBy(payment => payment.Status)
            .Select(group => new PaymentStatusTotal(group.Key, group.Count(), group.Sum(payment => payment.AmountMinor)))
            .ToListAsync(cancellationToken);
        var providers = await db.Payments.AsNoTracking().Select(payment => payment.Provider).Distinct().OrderBy(provider => provider).ToListAsync(cancellationToken);

        return new PaymentsOverview(
            new PagedResult<PaymentRow>(rows, pageNumber, AdminPaging.DefaultPageSize, total),
            Enum.GetValues<PaymentStatus>().Select(value => totals.FirstOrDefault(entry => entry.Status == value) ?? new PaymentStatusTotal(value, 0, 0)).ToList(),
            providers);
    }

    private static IQueryable<Payment> ApplyScope(IQueryable<Payment> query, PaymentFilter filter)
    {
        if (filter.Method is { } method)
        {
            query = query.Where(payment => payment.Method == method);
        }

        if (!string.IsNullOrWhiteSpace(filter.Provider))
        {
            var provider = filter.Provider;
            query = query.Where(payment => payment.Provider == provider);
        }

        if (filter.From is { } from)
        {
            var start = LocalDays.StartOfDay(from);
            query = query.Where(payment => payment.CreatedAt >= start);
        }

        if (filter.To is { } to)
        {
            var end = LocalDays.StartOfDay(to.AddDays(1));
            query = query.Where(payment => payment.CreatedAt < end);
        }

        return query;
    }
}

public enum CustomerSegment
{
    NoPurchases,
    New,
    Recurring,
}

public static class CustomerSegments
{
    public const int RecurringThreshold = 2;

    public static CustomerSegment For(int paidOrders) => paidOrders switch
    {
        <= 0 => CustomerSegment.NoPurchases,
        < RecurringThreshold => CustomerSegment.New,
        _ => CustomerSegment.Recurring,
    };

    public static string Label(CustomerSegment segment) => segment switch
    {
        CustomerSegment.NoPurchases => "Sem compras",
        CustomerSegment.New => "Novo",
        _ => "Recorrente",
    };
}

/// <summary>Row of the customer list with purchase figures.</summary>
public sealed record CustomerRow(int Id, string Name, string? Phone, string? Email, int OrderCount, long TotalSpentMinor, DateTimeOffset? LastOrderAt, CustomerSegment Segment);

/// <summary>Customer with every order.</summary>
public sealed record CustomerDetail(CustomerRow Customer, DateTimeOffset CreatedAt, IReadOnlyList<OrderSummary> Orders);

public interface IAdminCustomers
{
    /// <summary>Lists customers with order count, total spent and recurrence.</summary>
    Task<PagedResult<CustomerRow>> ListAsync(string? search, int page, CancellationToken cancellationToken = default);

    /// <summary>Loads one customer with orders, or null when it does not exist.</summary>
    Task<CustomerDetail?> GetAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class AdminCustomersService(IDbContextFactory<UnaykaDbContext> factory) : IAdminCustomers
{
    private sealed record OrderStats(int OrderCount, int PaidCount, long SpentMinor, DateTimeOffset LastOrderAt);

    public async Task<PagedResult<CustomerRow>> ListAsync(string? search, int page, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.Users.AsNoTracking().Where(user => user.Role == UserRole.Customer);
        if (SearchPattern.Contains(search) is { } pattern)
        {
            query = query.Where(user => EF.Functions.Like(user.Name, pattern) || EF.Functions.Like(user.Phone, pattern) || EF.Functions.Like(user.Email, pattern));
        }

        var pageNumber = AdminPaging.NormalizePage(page);
        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.Name)
            .Skip((pageNumber - 1) * AdminPaging.DefaultPageSize)
            .Take(AdminPaging.DefaultPageSize)
            .ToListAsync(cancellationToken);
        var stats = await LoadStatsAsync(db, users.Select(user => user.Id).ToList(), cancellationToken);
        var rows = users.Select(user => ToRow(user, stats.GetValueOrDefault(user.Id))).ToList();
        return new PagedResult<CustomerRow>(rows, pageNumber, AdminPaging.DefaultPageSize, total);
    }

    public async Task<CustomerDetail?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.Role == UserRole.Customer, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var stats = await LoadStatsAsync(db, [user.Id], cancellationToken);
        var orders = await db.Orders.AsNoTracking()
            .Where(order => order.UserId == user.Id)
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new OrderSummary(order.Id, order.Number, order.CustomerName, order.Channel, order.Status, order.TotalMinor, order.CreatedAt))
            .ToListAsync(cancellationToken);
        return new CustomerDetail(ToRow(user, stats.GetValueOrDefault(user.Id)), user.CreatedAt, orders);
    }

    private static async Task<Dictionary<int, OrderStats>> LoadStatsAsync(UnaykaDbContext db, List<int> userIds, CancellationToken cancellationToken)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(order => order.UserId != null && userIds.Contains(order.UserId.Value))
            .Select(order => new { UserId = order.UserId!.Value, order.Status, order.TotalMinor, order.CreatedAt })
            .ToListAsync(cancellationToken);
        return orders
            .GroupBy(order => order.UserId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var paid = group.Where(order => AdminOrderGroups.Revenue.Contains(order.Status)).ToList();
                    return new OrderStats(group.Count(), paid.Count, paid.Sum(order => order.TotalMinor), group.Max(order => order.CreatedAt));
                });
    }

    private static CustomerRow ToRow(User user, OrderStats? stats) => new(
        user.Id,
        user.Name,
        user.Phone,
        user.Email,
        stats?.OrderCount ?? 0,
        stats?.SpentMinor ?? 0,
        stats?.LastOrderAt,
        CustomerSegments.For(stats?.PaidCount ?? 0));
}

/// <summary>Minimal order data needed to compute dashboard figures.</summary>
public sealed record OrderSample(DateTimeOffset CreatedAt, OrderStatus Status, long TotalMinor);

/// <summary>Sales total of one local day.</summary>
public sealed record DailySales(DateOnly Day, long TotalMinor);

/// <summary>Headline dashboard figures.</summary>
public sealed record DashboardFigures(
    long TodaySalesMinor,
    long MonthSalesMinor,
    int PendingOrders,
    long AverageTicketMinor,
    IReadOnlyList<DailySales> Daily);

public static class DashboardCalculator
{
    public const int MonthDays = 30;
    public const int ChartDays = 14;

    public static DashboardFigures Compute(IEnumerable<OrderSample> orders, DateTimeOffset now)
    {
        var samples = orders.ToList();
        var today = LocalDays.ToLocalDay(now);
        var monthStart = today.AddDays(1 - MonthDays);
        var paid = samples.Where(sample => AdminOrderGroups.Revenue.Contains(sample.Status)).ToList();
        var paidThisMonth = paid.Where(sample => LocalDays.ToLocalDay(sample.CreatedAt) >= monthStart).ToList();
        var monthTotal = paidThisMonth.Sum(sample => sample.TotalMinor);

        return new DashboardFigures(
            paid.Where(sample => LocalDays.ToLocalDay(sample.CreatedAt) == today).Sum(sample => sample.TotalMinor),
            monthTotal,
            samples.Count(sample => AdminOrderGroups.Open.Contains(sample.Status)),
            paidThisMonth.Count == 0 ? 0 : monthTotal / paidThisMonth.Count,
            BuildDaily(paid, today));
    }

    private static List<DailySales> BuildDaily(List<OrderSample> paid, DateOnly today)
    {
        var totalsByDay = paid
            .GroupBy(sample => LocalDays.ToLocalDay(sample.CreatedAt))
            .ToDictionary(group => group.Key, group => group.Sum(sample => sample.TotalMinor));

        return Enumerable.Range(0, ChartDays)
            .Select(offset => today.AddDays(offset + 1 - ChartDays))
            .Select(day => new DailySales(day, totalsByDay.GetValueOrDefault(day)))
            .ToList();
    }
}

/// <summary>Best selling product in the last 30 days.</summary>
public sealed record TopProduct(int ProductId, string Name, int Quantity, long RevenueMinor);

/// <summary>Order line shown in compact lists.</summary>
public sealed record OrderSummary(int Id, string Number, string CustomerName, OrderChannel Channel, OrderStatus Status, long TotalMinor, DateTimeOffset CreatedAt);

/// <summary>Active product with little stock left.</summary>
public sealed record LowStockProduct(int Id, string Name, int StockQuantity, string? ImageUrl);

/// <summary>Everything the dashboard page shows.</summary>
public sealed record DashboardData(
    DashboardFigures Figures,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<OrderSummary> LatestOrders,
    IReadOnlyList<LowStockProduct> LowStock);

public interface IAdminDashboard
{
    /// <summary>Loads figures, rankings and alerts for the dashboard.</summary>
    Task<DashboardData> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminDashboardService(IDbContextFactory<UnaykaDbContext> factory, TimeProvider time) : IAdminDashboard
{
    public const int LowStockThreshold = 5;
    private const int TopProductCount = 5;
    private const int LatestOrderCount = 8;
    private const int LowStockCount = 8;

    public async Task<DashboardData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var since = LocalDays.StartOfDay(LocalDays.ToLocalDay(now).AddDays(1 - DashboardCalculator.MonthDays));
        var open = AdminOrderGroups.Open;

        var samples = await db.Orders.AsNoTracking()
            .Where(order => order.CreatedAt >= since || open.Contains(order.Status))
            .Select(order => new OrderSample(order.CreatedAt, order.Status, order.TotalMinor))
            .ToListAsync(cancellationToken);

        return new DashboardData(
            DashboardCalculator.Compute(samples, now),
            await LoadTopProductsAsync(db, since, cancellationToken),
            await LoadLatestOrdersAsync(db, cancellationToken),
            await LoadLowStockAsync(db, cancellationToken));
    }

    private static async Task<IReadOnlyList<TopProduct>> LoadTopProductsAsync(UnaykaDbContext db, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var revenueStatuses = AdminOrderGroups.Revenue;
        return await (
                from item in db.OrderItems
                join order in db.Orders on item.OrderId equals order.Id
                where revenueStatuses.Contains(order.Status) && order.CreatedAt >= since
                group item by new { item.ProductId, item.ProductName }
                into grouped
                orderby grouped.Sum(item => item.Quantity) descending
                select new TopProduct(
                    grouped.Key.ProductId,
                    grouped.Key.ProductName,
                    grouped.Sum(item => item.Quantity),
                    grouped.Sum(item => item.UnitPriceMinor * item.Quantity)))
            .Take(TopProductCount)
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<OrderSummary>> LoadLatestOrdersAsync(UnaykaDbContext db, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .OrderByDescending(order => order.CreatedAt)
            .Take(LatestOrderCount)
            .Select(order => new OrderSummary(order.Id, order.Number, order.CustomerName, order.Channel, order.Status, order.TotalMinor, order.CreatedAt))
            .ToListAsync(cancellationToken);

    private static async Task<IReadOnlyList<LowStockProduct>> LoadLowStockAsync(UnaykaDbContext db, CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking()
            .Where(product => product.Status == CatalogStatus.Active
                              && product.Kind == ProductKind.Physical
                              && product.Stocks.Sum(stock => stock.Quantity) <= LowStockThreshold)
            .OrderBy(product => product.Stocks.Sum(stock => stock.Quantity))
            .ThenBy(product => product.Name)
            .Take(LowStockCount)
            .Select(product => new LowStockProduct(
                product.Id,
                product.Name,
                product.Stocks.Sum(stock => stock.Quantity),
                product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault()))
            .ToListAsync(cancellationToken);
}

/// <summary>Audit entry with the name of the actor.</summary>
public sealed record AuditRow(int Id, DateTimeOffset At, string ActorName, string Action, string EntityType, string EntityId, string Summary);

public interface IAdminAudit
{
    /// <summary>Lists audit entries newest first.</summary>
    Task<PagedResult<AuditRow>> ListAsync(int page, CancellationToken cancellationToken = default);
}

public sealed class AdminAuditService(IDbContextFactory<UnaykaDbContext> factory) : IAdminAudit
{
    private const string SystemActor = "Sistema";

    public async Task<PagedResult<AuditRow>> ListAsync(int page, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var pageNumber = AdminPaging.NormalizePage(page);
        var total = await db.AuditEntries.CountAsync(cancellationToken);
        var entries = await db.AuditEntries.AsNoTracking()
            .OrderByDescending(entry => entry.At)
            .ThenByDescending(entry => entry.Id)
            .Skip((pageNumber - 1) * AdminPaging.DefaultPageSize)
            .Take(AdminPaging.DefaultPageSize)
            .ToListAsync(cancellationToken);
        var actorIds = entries.Where(entry => entry.ActorUserId != null).Select(entry => entry.ActorUserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);
        var rows = entries
            .Select(entry => new AuditRow(
                entry.Id,
                entry.At,
                entry.ActorUserId is { } actorId ? names.GetValueOrDefault(actorId, SystemActor) : SystemActor,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.Summary))
            .ToList();
        return new PagedResult<AuditRow>(rows, pageNumber, AdminPaging.DefaultPageSize, total);
    }
}

/// <summary>Row of the coupon list with usage and revenue.</summary>
public sealed record CouponRow(
    int Id,
    string Code,
    string Description,
    CouponKind Kind,
    long Value,
    int UsedCount,
    int? UsageLimit,
    long RevenueMinor,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);

/// <summary>Editable coupon. For Percentage the value is a percent, for Fixed it is MZN.</summary>
public sealed class CouponForm
{
    public int? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CouponKind Kind { get; set; }
    public decimal Value { get; set; }
    public decimal MinSubtotal { get; set; }
    public int? CategoryId { get; set; }
    public DateTime? StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
    public int? UsageLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class CouponValues
{
    private const int BasisPointsPerPercent = 100;
    private const decimal MaxPercent = 100;

    public static long ToStoredValue(CouponKind kind, decimal input) =>
        kind == CouponKind.Percentage ? (long)Math.Round(input * BasisPointsPerPercent, MidpointRounding.AwayFromZero) : MoneyInput.ToMinor(input);

    public static decimal ToInputValue(CouponKind kind, long stored) =>
        kind == CouponKind.Percentage ? stored / (decimal)BasisPointsPerPercent : MoneyInput.ToAmount(stored);

    public static List<string> Validate(CouponForm form)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(form.Code) || form.Code.Trim().Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            errors.Add("O código deve ter apenas letras e números.");
        }

        if (form.Value <= 0 || (form.Kind == CouponKind.Percentage && form.Value > MaxPercent))
        {
            errors.Add(form.Kind == CouponKind.Percentage ? "A percentagem deve estar entre 1 e 100." : "O valor do desconto deve ser superior a zero.");
        }

        if (form.MinSubtotal < 0)
        {
            errors.Add("O subtotal mínimo não pode ser negativo.");
        }

        if (form.UsageLimit is <= 0)
        {
            errors.Add("O limite de utilizações deve ser superior a zero.");
        }

        if (form.StartsOn is { } start && form.EndsOn is { } end && end < start)
        {
            errors.Add("A data de fim deve ser posterior à data de início.");
        }

        return errors;
    }
}

public interface IAdminCoupons
{
    /// <summary>Lists coupons with usage counters and revenue generated.</summary>
    Task<IReadOnlyList<CouponRow>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads a coupon for editing, or null when it does not exist.</summary>
    Task<CouponForm?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a coupon after validation and audits the change.</summary>
    Task<AdminResult> SaveAsync(int? actorUserId, CouponForm form, CancellationToken cancellationToken = default);

    /// <summary>Pauses or resumes a coupon and audits the change.</summary>
    Task<AdminResult> SetActiveAsync(int? actorUserId, int id, bool isActive, CancellationToken cancellationToken = default);
}

public sealed class AdminCouponsService(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit, TimeProvider time) : IAdminCoupons
{
    public async Task<IReadOnlyList<CouponRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var revenueStatuses = AdminOrderGroups.Revenue;
        var revenueByCode = await db.Orders.AsNoTracking()
            .Where(order => order.CouponCode != null && revenueStatuses.Contains(order.Status))
            .GroupBy(order => order.CouponCode!)
            .Select(group => new { Code = group.Key, Revenue = group.Sum(order => order.TotalMinor) })
            .ToDictionaryAsync(entry => entry.Code, entry => entry.Revenue, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var coupons = await db.Coupons.AsNoTracking().OrderByDescending(coupon => coupon.IsActive).ThenBy(coupon => coupon.Code).ToListAsync(cancellationToken);
        return coupons
            .Select(coupon => new CouponRow(
                coupon.Id,
                coupon.Code,
                coupon.Description,
                coupon.Kind,
                coupon.Value,
                coupon.UsedCount,
                coupon.UsageLimit,
                revenueByCode.GetValueOrDefault(coupon.Code),
                coupon.StartsAt,
                coupon.EndsAt,
                coupon.IsActive))
            .ToList();
    }

    public async Task<CouponForm?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var coupon = await db.Coupons.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return coupon is null
            ? null
            : new CouponForm
            {
                Id = coupon.Id,
                Code = coupon.Code,
                Description = coupon.Description,
                Kind = coupon.Kind,
                Value = CouponValues.ToInputValue(coupon.Kind, coupon.Value),
                MinSubtotal = MoneyInput.ToAmount(coupon.MinSubtotalMinor),
                CategoryId = coupon.CategoryId,
                StartsOn = coupon.StartsAt is { } startsAt ? LocalDays.ToLocalDay(startsAt).ToDateTime(TimeOnly.MinValue) : null,
                EndsOn = coupon.EndsAt is { } endsAt ? LocalDays.ToLocalDay(endsAt).ToDateTime(TimeOnly.MinValue) : null,
                UsageLimit = coupon.UsageLimit,
                IsActive = coupon.IsActive,
            };
    }

    public async Task<AdminResult> SaveAsync(int? actorUserId, CouponForm form, CancellationToken cancellationToken = default)
    {
        form.Code = form.Code.Trim().ToUpperInvariant();
        var errors = CouponValues.Validate(form);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.Coupons.AnyAsync(coupon => coupon.Code == form.Code && coupon.Id != (form.Id ?? 0), cancellationToken))
        {
            errors.Add("Já existe um cupão com este código.");
        }

        if (form.CategoryId is { } categoryId && !await db.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            errors.Add("A categoria escolhida não existe.");
        }

        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        var coupon = form.Id is { } id ? await db.Coupons.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken) : null;
        if (form.Id is not null && coupon is null)
        {
            return AdminResult.Fail("Cupão não encontrado.");
        }

        var isNew = coupon is null;
        if (coupon is null)
        {
            coupon = new Coupon { CreatedAt = time.GetUtcNow() };
            db.Coupons.Add(coupon);
        }

        Apply(form, coupon);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            actorUserId,
            isNew ? AdminAuditActions.CouponCreated : AdminAuditActions.CouponUpdated,
            AdminEntityTypes.Coupon,
            coupon.Id.ToString(CultureInfo.InvariantCulture),
            isNew ? $"Cupão {coupon.Code} criado." : $"Cupão {coupon.Code} atualizado.",
            cancellationToken);
        return AdminResult.Ok(coupon.Id);
    }

    public async Task<AdminResult> SetActiveAsync(int? actorUserId, int id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var coupon = await db.Coupons.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (coupon is null)
        {
            return AdminResult.Fail("Cupão não encontrado.");
        }

        coupon.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            actorUserId,
            AdminAuditActions.CouponToggled,
            AdminEntityTypes.Coupon,
            coupon.Id.ToString(CultureInfo.InvariantCulture),
            isActive ? $"Cupão {coupon.Code} retomado." : $"Cupão {coupon.Code} pausado.",
            cancellationToken);
        return AdminResult.Ok(coupon.Id);
    }

    private static void Apply(CouponForm form, Coupon coupon)
    {
        coupon.Code = form.Code;
        coupon.Description = form.Description.Trim();
        coupon.Kind = form.Kind;
        coupon.Value = CouponValues.ToStoredValue(form.Kind, form.Value);
        coupon.MinSubtotalMinor = MoneyInput.ToMinor(form.MinSubtotal);
        coupon.CategoryId = form.CategoryId;
        coupon.StartsAt = form.StartsOn is { } start ? LocalDays.StartOfDay(DateOnly.FromDateTime(start)) : null;
        coupon.EndsAt = form.EndsOn is { } end ? LocalDays.StartOfDay(DateOnly.FromDateTime(end).AddDays(1)).AddTicks(-1) : null;
        coupon.UsageLimit = form.UsageLimit;
        coupon.IsActive = form.IsActive;
    }
}
