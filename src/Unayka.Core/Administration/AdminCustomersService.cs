using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Data;

namespace Unayka.Core.Administration;

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
