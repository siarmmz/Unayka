namespace Unayka.Core.Payments;

public static class OnlineMethods
{
    public static readonly PaymentMethod[] All = [.. WalletMethods.All, PaymentMethod.Card];

    public static bool IsOnline(this PaymentMethod method) => All.Contains(method);
}
