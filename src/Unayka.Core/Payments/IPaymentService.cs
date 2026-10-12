using Unayka.Core.Orders;

namespace Unayka.Core.Payments;

public enum PaymentRetryOutcome
{
    Started,
    InvalidPhone,
    NotRetryable,
}

public interface IPaymentService
{
    /// <summary>Requests a wallet push for the order and attaches a pending payment to it.</summary>
    Task<Payment> StartAsync(Order order, PaymentMethod method, string? phone, CancellationToken cancellationToken = default);

    /// <summary>Resolves pending payments of the order with the gateway and marks the order paid when confirmed.</summary>
    Task RefreshAsync(string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>Creates a new payment for an order whose last payment failed.</summary>
    Task<PaymentRetryOutcome> RetryAsync(string orderNumber, PaymentMethod method, string? phone, CancellationToken cancellationToken = default);
}
