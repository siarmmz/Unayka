namespace Unayka.Core.Payments;

/// <summary>Wallet push request sent to the provider.</summary>
public sealed record PaymentRequest(string OrderNumber, long AmountMinor, PaymentMethod Method, string Phone);

/// <summary>Provider acknowledgement of a wallet push.</summary>
public sealed record PaymentRequestResult(string ProviderReference);

/// <summary>What the gateway needs to know about a payment to resolve its status.</summary>
public sealed record PaymentStatusQuery(string ProviderReference, string? Phone, DateTimeOffset CreatedAt);

/// <summary>Status reported by the provider, with the reason when the payment failed.</summary>
public sealed record PaymentStatusResult(PaymentStatus Status, string? FailureReason);

public interface IPaymentGateway
{
    /// <summary>Short provider name stored on each payment.</summary>
    string Provider { get; }

    /// <summary>Asks the customer wallet to confirm the amount.</summary>
    Task<PaymentRequestResult> RequestPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads the current status of a payment previously requested.</summary>
    Task<PaymentStatusResult> GetStatusAsync(PaymentStatusQuery query, CancellationToken cancellationToken = default);
}
