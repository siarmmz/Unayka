using Unayka.Core.Orders;

namespace Unayka.Core.Payments;

/// <summary>Provider agnostic payment attempt for an order. Amounts are in minor units.</summary>
public sealed class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public string Provider { get; set; } = string.Empty;
    public PaymentMethod Method { get; set; }
    public string? Phone { get; set; }
    public long AmountMinor { get; set; }
    public long FeeMinor { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ProviderReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<PaymentEvent> Events { get; set; } = [];
}
