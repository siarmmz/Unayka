using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Data;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Core.Counter;

public sealed class CounterShifts(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit, TimeProvider time) : ICounterShifts
{
    private const string ShiftEntity = "CashShift";

    public async Task<CashShift?> GetOpenAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        return await database.CashShifts.AsNoTracking().FirstOrDefaultAsync(shift => shift.ClosedAt == null, cancellationToken);
    }

    public async Task<CashShift> OpenAsync(int userId, long openingMinor, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.CashShifts.FirstOrDefaultAsync(shift => shift.ClosedAt == null, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var shift = new CashShift { OpenedByUserId = userId, OpenedAt = time.GetUtcNow(), OpeningMinor = Math.Max(0, openingMinor) };
        database.CashShifts.Add(shift);
        await database.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(userId, "shift.open", ShiftEntity, shift.Id.ToString(CultureInfo.InvariantCulture), $"Opening {Formatters.FormatMoney(shift.OpeningMinor)}", cancellationToken);
        return shift;
    }

    public async Task<ShiftSummary> SummarizeAsync(CashShift shift, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var end = shift.ClosedAt ?? time.GetUtcNow();
        var payments = await database.Payments.AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Paid
                              && payment.Order!.Channel == OrderChannel.Counter
                              && payment.Order.CreatedAt >= shift.OpenedAt
                              && payment.Order.CreatedAt <= end)
            .Select(payment => new { payment.Method, payment.AmountMinor })
            .ToListAsync(cancellationToken);
        var totals = CashMath.Summarize(shift.OpeningMinor, payments.Select(payment => (payment.Method, payment.AmountMinor)));
        return new ShiftSummary(shift, totals);
    }

    public async Task<ShiftSummary?> CloseAsync(int userId, long countedCashMinor, string? note, CancellationToken cancellationToken = default)
    {
        var open = await GetOpenAsync(cancellationToken);
        if (open is null)
        {
            return null;
        }

        var summary = await SummarizeAsync(open, cancellationToken);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var shift = await database.CashShifts.SingleAsync(candidate => candidate.Id == open.Id, cancellationToken);
        shift.ClosedAt = time.GetUtcNow();
        shift.CountedMinor = countedCashMinor;
        shift.ExpectedMinor = summary.Totals.ExpectedCashMinor;
        await database.SaveChangesAsync(cancellationToken);

        var difference = CashMath.Difference(countedCashMinor, summary.Totals.ExpectedCashMinor);
        var detail = string.IsNullOrWhiteSpace(note) ? string.Empty : $" {note.Trim()}";
        await audit.WriteAsync(userId, "shift.close", ShiftEntity, shift.Id.ToString(CultureInfo.InvariantCulture), $"Cash difference {Formatters.FormatMoney(difference)}.{detail}", cancellationToken);
        return new ShiftSummary(shift, summary.Totals);
    }

    public async Task<IReadOnlyList<ShiftHistoryRow>> GetHistoryAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var shifts = await database.CashShifts.AsNoTracking()
            .Where(shift => shift.ClosedAt != null)
            .OrderByDescending(shift => shift.OpenedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
        return shifts
            .Select(shift => new ShiftHistoryRow(shift.Id, shift.OpenedAt, shift.ClosedAt, shift.OpeningMinor, shift.ExpectedMinor ?? 0, shift.CountedMinor))
            .ToList();
    }
}
