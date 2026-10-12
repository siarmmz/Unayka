using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;
using Unayka.Core.Payments;

namespace Unayka.Core.Administration;

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
