using Unayka.Core.Catalog;
using Unayka.Core.Coupons;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Web.Components.Admin;

public sealed record SelectOption(string Value, string Label);

public static class AdminOptions
{
    private const string AllLabel = "Todos";

    public static readonly SelectOption All = new(string.Empty, AllLabel);

    public static readonly IReadOnlyList<SelectOption> OrderStatuses =
        [All, .. Enum.GetValues<OrderStatus>().Select(status => new SelectOption(status.ToString(), StatusLabels.For(status).Label))];

    public static readonly IReadOnlyList<SelectOption> Channels =
        [All, new(nameof(OrderChannel.Online), "Online"), new(nameof(OrderChannel.Counter), "Balcão")];

    public static readonly IReadOnlyList<SelectOption> PaymentStatuses =
        [All, .. Enum.GetValues<PaymentStatus>().Select(status => new SelectOption(status.ToString(), StatusLabels.For(status).Label))];

    public static readonly IReadOnlyList<SelectOption> PaymentMethods =
        [All, .. Enum.GetValues<PaymentMethod>().Select(method => new SelectOption(method.ToString(), StatusLabels.For(method)))];

    public static readonly IReadOnlyList<SelectOption> CategoryKinds =
        [new(nameof(CategoryKind.Tea), "Chás e cafés"), new(nameof(CategoryKind.Cosmetic), "Cosméticos")];

    public static readonly IReadOnlyList<SelectOption> MediaKinds =
    [
        new(nameof(ProductMediaKind.Prepared), "Preparado"),
        new(nameof(ProductMediaKind.Package), "Embalagem"),
        new(nameof(ProductMediaKind.Elements), "Elementos"),
        new(nameof(ProductMediaKind.Gallery), "Galeria"),
    ];

    public static readonly IReadOnlyList<SelectOption> CouponKinds =
        [new(nameof(CouponKind.Percentage), "Percentagem"), new(nameof(CouponKind.Fixed), "Valor fixo")];

    public static T? ParseOrNull<T>(string value)
        where T : struct, Enum =>
        Enum.TryParse<T>(value, out var parsed) ? parsed : null;
}
