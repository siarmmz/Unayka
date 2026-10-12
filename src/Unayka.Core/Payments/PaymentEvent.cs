namespace Unayka.Core.Payments;

/// <summary>Raw notification or state change received for a payment.</summary>
public sealed class PaymentEvent
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }
}
