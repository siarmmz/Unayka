using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Unayka.Core.Orders;

namespace Unayka.Core.Administration;

/// <summary>One page of a larger result set.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>Outcome of an administration command with user facing messages.</summary>
public sealed record AdminResult(bool Succeeded, IReadOnlyList<string> Errors, int? EntityId = null)
{
    public static AdminResult Ok(int? entityId = null) => new(true, [], entityId);

    public static AdminResult Fail(params string[] errors) => new(false, errors);

    public static AdminResult Fail(IReadOnlyList<string> errors) => new(false, errors);
}

public static class SearchPattern
{
    public static string? Contains(string? term)
    {
        var cleaned = (term ?? string.Empty).Replace("%", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).Trim();
        return cleaned.Length == 0 ? null : $"%{cleaned}%";
    }
}

public static class AdminPaging
{
    public const int DefaultPageSize = 15;

    public static int NormalizePage(int page) => Math.Max(1, page);
}

public static class AdminOrderGroups
{
    public static readonly IReadOnlyList<OrderStatus> Revenue =
        [OrderStatus.Paid, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Delivered];

    public static readonly IReadOnlyList<OrderStatus> Open =
        [OrderStatus.New, OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.Preparing, OrderStatus.Ready];
}

public static class LocalDays
{
    private static readonly TimeSpan MaputoOffset = TimeSpan.FromHours(2);

    public static DateOnly ToLocalDay(DateTimeOffset value) =>
        DateOnly.FromDateTime(value.ToOffset(MaputoOffset).DateTime);

    public static string FormatDateTime(DateTimeOffset value) =>
        value.ToOffset(MaputoOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static DateTimeOffset StartOfDay(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), MaputoOffset);
}

public static class MoneyInput
{
    private const int MinorUnitsPerUnit = 100;

    public static long ToMinor(decimal amount) =>
        (long)Math.Round(amount * MinorUnitsPerUnit, MidpointRounding.AwayFromZero);

    public static decimal ToAmount(long minorUnits) => minorUnits / (decimal)MinorUnitsPerUnit;
}

public static partial class Slugs
{
    public const int MaxLength = 80;

    public static string FromName(string? name)
    {
        var decomposed = (name ?? string.Empty).Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder();
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(char.ToLowerInvariant(character));
            }
        }

        var slug = NonSlugCharacters().Replace(plain.ToString(), "-").Trim('-');
        return slug.Length > MaxLength ? slug[..MaxLength].Trim('-') : slug;
    }

    public static bool IsValid(string? slug) => !string.IsNullOrEmpty(slug) && slug.Length <= MaxLength && ValidSlug().IsMatch(slug);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidSlug();
}

public static class AdminAuditActions
{
    public const string ProductCreated = "product.created";
    public const string ProductUpdated = "product.updated";
    public const string ProductToggled = "product.toggled";
    public const string CategoryCreated = "category.created";
    public const string CategoryUpdated = "category.updated";
    public const string CouponCreated = "coupon.created";
    public const string CouponUpdated = "coupon.updated";
    public const string CouponToggled = "coupon.toggled";
    public const string OrderStatusChanged = "order.status_changed";
}

public static class AdminEntityTypes
{
    public const string Product = "Product";
    public const string Category = "Category";
    public const string Coupon = "Coupon";
    public const string Order = "Order";
}
