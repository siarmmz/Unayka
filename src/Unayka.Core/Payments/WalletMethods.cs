namespace Unayka.Core.Payments;

public static class WalletMethods
{
    public static readonly PaymentMethod[] All = [PaymentMethod.MPesa, PaymentMethod.EMola, PaymentMethod.MKesh];

    public static bool IsWallet(this PaymentMethod method) => All.Contains(method);
}
