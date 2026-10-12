using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Coupons;
using Unayka.Core.Data;

namespace Unayka.Core.Administration;

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
