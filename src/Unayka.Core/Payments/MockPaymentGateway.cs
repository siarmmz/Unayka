namespace Unayka.Core.Payments;

public sealed class MockPaymentGateway(TimeProvider time) : IPaymentGateway
{
    public const string ProviderName = "mock";
    public const string InsufficientFundsReason = "Saldo insuficiente";
    public const char FailingPhoneEnding = '0';

    private static readonly TimeSpan PaidAfter = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FailedAfter = TimeSpan.FromSeconds(3);

    public string Provider => ProviderName;

    public Task<PaymentRequestResult> RequestPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentRequestResult($"MOCK-{Guid.NewGuid():N}"[..17].ToUpperInvariant()));

    public Task<PaymentStatusResult> GetStatusAsync(PaymentStatusQuery query, CancellationToken cancellationToken = default)
    {
        var elapsed = time.GetUtcNow() - query.CreatedAt;
        var failing = query.Phone?.EndsWith(FailingPhoneEnding) == true;
        var result = (failing, elapsed) switch
        {
            (true, _) when elapsed >= FailedAfter => new PaymentStatusResult(PaymentStatus.Failed, InsufficientFundsReason),
            (false, _) when elapsed >= PaidAfter => new PaymentStatusResult(PaymentStatus.Paid, null),
            _ => new PaymentStatusResult(PaymentStatus.Pending, null),
        };
        return Task.FromResult(result);
    }
}
