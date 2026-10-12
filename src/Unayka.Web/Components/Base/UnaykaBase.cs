using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Unayka.Core.Administration;
using Unayka.Core.Catalog;
using Unayka.Core.Configuration;
using Unayka.Core.Coupons;
using Unayka.Core.Locations;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Web.Components.Base;

public static class UnaykaView
{
    private const string BadgeBase = "inline-flex items-center rounded-full px-3 py-1 text-xs font-medium ";

    private static readonly Dictionary<StatusTone, string> ToneClasses = new()
    {
        [StatusTone.Neutral] = BadgeBase + "bg-surface-alt text-muted",
        [StatusTone.Info] = BadgeBase + "bg-success-soft text-success",
        [StatusTone.Success] = BadgeBase + "bg-success-soft text-success",
        [StatusTone.Warning] = BadgeBase + "bg-warning-soft text-warning",
        [StatusTone.Danger] = BadgeBase + "bg-accent-soft text-danger",
    };

    private static readonly Dictionary<CatalogStatus, (string Label, string CssClass)> CatalogBadges = new()
    {
        [CatalogStatus.Active] = ("Ativo", BadgeBase + "bg-success-soft text-accent-strong"),
        [CatalogStatus.Inactive] = ("Inativo", BadgeBase + "bg-surface-alt text-muted"),
        [CatalogStatus.Archived] = ("Arquivado", BadgeBase + "bg-accent-soft text-accent"),
    };

    public static MarkupString Text(string? value, bool format = false) => new(SafeText.ToHtml(value ?? string.Empty, format));

    public static string ToUrl(string? value) => SafeUrl.ToUrl(value);

    public static string ToUrlTarget(string? value) => SafeUrl.ToTarget(value, AppEnvConfig.PublicBaseUrl);

    public static string FormatMoney(long minorUnits) => Formatters.FormatMoney(minorUnits);

    public static string FormatDate(DateTimeOffset value) => Formatters.FormatDate(value);

    public static string FormatPhone(string? value) => Formatters.FormatPhone(value);

    public static (string Label, string CssClass) OrderBadge(OrderStatus status) => Badge(StatusLabels.For(status));

    public static (string Label, string CssClass) PaymentBadge(PaymentStatus status) => Badge(StatusLabels.For(status));

    public static (string Label, string CssClass) CatalogBadge(CatalogStatus status) => CatalogBadges[status];

    private static (string Label, string CssClass) Badge((string Label, StatusTone Tone) status) => (status.Label, ToneClasses[status.Tone]);
}

public abstract class UnaykaComponentBase : ComponentBase
{
    protected static MarkupString Text(string? value, bool format = false) => UnaykaView.Text(value, format);

    protected static string ToUrl(string? value) => UnaykaView.ToUrl(value);

    protected static string ToUrlTarget(string? value) => UnaykaView.ToUrlTarget(value);

    protected static string FormatMoney(long minorUnits) => UnaykaView.FormatMoney(minorUnits);

    protected static string FormatDate(DateTimeOffset value) => UnaykaView.FormatDate(value);

    protected static string FormatPhone(string? value) => UnaykaView.FormatPhone(value);

    protected static (string Label, string CssClass) OrderBadge(OrderStatus status) => UnaykaView.OrderBadge(status);

    protected static (string Label, string CssClass) PaymentBadge(PaymentStatus status) => UnaykaView.PaymentBadge(status);

    protected static (string Label, string CssClass) CatalogBadge(CatalogStatus status) => UnaykaView.CatalogBadge(status);
}

public abstract class UnaykaLayoutBase : LayoutComponentBase
{
    protected static MarkupString Text(string? value, bool format = false) => UnaykaView.Text(value, format);

    protected static string ToUrl(string? value) => UnaykaView.ToUrl(value);

    protected static string ToUrlTarget(string? value) => UnaykaView.ToUrlTarget(value);

    protected static string FormatMoney(long minorUnits) => UnaykaView.FormatMoney(minorUnits);

    protected static string FormatDate(DateTimeOffset value) => UnaykaView.FormatDate(value);

    protected static string FormatPhone(string? value) => UnaykaView.FormatPhone(value);
}

/// <summary>Option of a select box in the staff pages.</summary>
public sealed record SelectOption(string Value, string Label);

public abstract class StaffPageBase : UnaykaComponentBase
{
    private const string AllLabel = "Todos";

    protected static readonly SelectOption All = new(string.Empty, AllLabel);

    protected static readonly IReadOnlyList<SelectOption> OrderStatuses =
        [All, .. Enum.GetValues<OrderStatus>().Select(status => new SelectOption(status.ToString(), StatusLabels.For(status).Label))];

    protected static readonly IReadOnlyList<SelectOption> Channels =
        [All, new(nameof(OrderChannel.Online), "Online"), new(nameof(OrderChannel.Counter), "Balcão")];

    protected static readonly IReadOnlyList<SelectOption> PaymentStatuses =
        [All, .. Enum.GetValues<PaymentStatus>().Select(status => new SelectOption(status.ToString(), StatusLabels.For(status).Label))];

    protected static readonly IReadOnlyList<SelectOption> PaymentMethods =
        [All, .. Enum.GetValues<PaymentMethod>().Select(method => new SelectOption(method.ToString(), StatusLabels.For(method)))];

    protected static readonly IReadOnlyList<SelectOption> ProductKinds =
        [new(nameof(ProductKind.Physical), "Físico"), new(nameof(ProductKind.Digital), "Digital")];

    protected static readonly IReadOnlyList<SelectOption> CatalogStatuses =
        [new(nameof(CatalogStatus.Active), "Ativo"), new(nameof(CatalogStatus.Inactive), "Inativo"), new(nameof(CatalogStatus.Archived), "Arquivado")];

    protected static readonly IReadOnlyList<SelectOption> ContactKinds =
        [new(nameof(ContactKind.Phone), "Telefone"), new(nameof(ContactKind.WhatsApp), "WhatsApp"), new(nameof(ContactKind.Email), "Email")];

    protected static readonly IReadOnlyList<SelectOption> MediaKinds =
    [
        new(nameof(ProductMediaKind.Prepared), "Preparado (chávena ou copo)"),
        new(nameof(ProductMediaKind.Package), "Embalagem ou produto"),
        new(nameof(ProductMediaKind.Jar), "Frasco ou lata"),
        new(nameof(ProductMediaKind.Elements), "Ingredientes"),
        new(nameof(ProductMediaKind.Applied), "Aplicação na pele ou cabelo"),
        new(nameof(ProductMediaKind.Cover), "Capa"),
        new(nameof(ProductMediaKind.Gallery), "Galeria"),
    ];

    protected static readonly IReadOnlyList<SelectOption> CouponKinds =
        [new(nameof(CouponKind.Percentage), "Percentagem"), new(nameof(CouponKind.Fixed), "Valor fixo")];

    protected static readonly (string Label, string Url)[] CounterLinks = [("Venda", "/balcao"), ("Caixa", "/balcao/caixa"), ("Preparação", "/balcao/preparacao")];

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    protected int? ActorUserId { get; private set; }

    protected int UserId => ActorUserId ?? 0;

    protected override async Task OnInitializedAsync()
    {
        var state = AuthenticationState is null ? null : await AuthenticationState;
        var claim = state?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        ActorUserId = int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    protected static T? ParseOrNull<T>(string value)
        where T : struct, Enum =>
        Enum.TryParse<T>(value, out var parsed) ? parsed : null;

    protected static string FormatDateTime(DateTimeOffset value) => LocalDays.FormatDateTime(value);

    protected static string FormatDay(DateOnly day) => day.ToString("dd/MM", CultureInfo.InvariantCulture);

    protected static string ChannelLabel(OrderChannel channel) => channel == OrderChannel.Online ? "Online" : "Balcão";

    protected static string FormatNumber(int value) => value.ToString(CultureInfo.InvariantCulture);
}
