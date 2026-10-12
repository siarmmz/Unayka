using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Web.Components.Checkout;

public sealed record PaymentIcon(string Url, string Alt);

public static class PaymentChoices
{
    private const string CardLabel = "Cartão Visa ou Mastercard";
    private const string IconFolder = "/images/pay/";

    private static readonly PaymentIcon Visa = new(IconFolder + "visa.svg", "Visa");
    private static readonly PaymentIcon Mastercard = new(IconFolder + "mastercard.svg", "Mastercard");

    public static readonly IReadOnlyList<PaymentIcon> AllIcons = [.. WalletMethods.All.SelectMany(Icons), Visa, Mastercard];

    public static string Label(PaymentMethod method) => method == PaymentMethod.Card ? CardLabel : StatusLabels.For(method);

    public static IReadOnlyList<PaymentIcon> Icons(PaymentMethod method) => method switch
    {
        PaymentMethod.MPesa => [new(IconFolder + "mpesa.png", "M-Pesa")],
        PaymentMethod.EMola => [new(IconFolder + "emola.png", "e-Mola")],
        PaymentMethod.MKesh => [new(IconFolder + "mkesh.png", "mKesh")],
        PaymentMethod.Card => [Visa, Mastercard],
        _ => [],
    };
}
