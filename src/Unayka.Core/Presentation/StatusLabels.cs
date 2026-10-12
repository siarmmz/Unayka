using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Presentation;

public enum StatusTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger,
}

public static class StatusLabels
{
    public static (string Label, StatusTone Tone) For(OrderStatus status) => status switch
    {
        OrderStatus.New => ("Novo", StatusTone.Info),
        OrderStatus.AwaitingPayment => ("A aguardar pagamento", StatusTone.Warning),
        OrderStatus.Paid => ("Pago", StatusTone.Success),
        OrderStatus.Preparing => ("Em preparação", StatusTone.Info),
        OrderStatus.Ready => ("Pronto", StatusTone.Success),
        OrderStatus.Delivered => ("Entregue", StatusTone.Neutral),
        OrderStatus.Cancelled => ("Cancelado", StatusTone.Danger),
        _ => (status.ToString(), StatusTone.Neutral),
    };

    public static (string Label, StatusTone Tone) For(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => ("Pendente", StatusTone.Warning),
        PaymentStatus.Paid => ("Pago", StatusTone.Success),
        PaymentStatus.Failed => ("Falhou", StatusTone.Danger),
        PaymentStatus.Cancelled => ("Cancelado", StatusTone.Neutral),
        _ => (status.ToString(), StatusTone.Neutral),
    };

    public static string For(PaymentMethod method) => method switch
    {
        PaymentMethod.MPesa => "M-Pesa",
        PaymentMethod.EMola => "e-Mola",
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.Card => "Cartão",
        _ => method.ToString(),
    };
}
