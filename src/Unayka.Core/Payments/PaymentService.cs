using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Data;
using Unayka.Core.Orders;

namespace Unayka.Core.Payments;

public sealed class PaymentService(
    UnaykaDbContext database,
    IPaymentGateway gateway,
    IAuditLog audit,
    TimeProvider time) : IPaymentService
{
    private const string OrderEntity = "Order";
    private const string RequestedEvent = "requested";
    private const string StatusEvent = "status";

    public async Task<Payment> StartAsync(
        Order order, PaymentMethod method, string phone, CancellationToken cancellationToken = default)
    {
        var requested = await gateway.RequestPaymentAsync(
            new PaymentRequest(order.Number, order.TotalMinor, method, phone), cancellationToken);
        var now = time.GetUtcNow();
        var payment = new Payment
        {
            Order = order,
            Provider = gateway.Provider,
            Method = method,
            Phone = phone,
            AmountMinor = order.TotalMinor,
            Status = PaymentStatus.Pending,
            ProviderReference = requested.ProviderReference,
            CreatedAt = now,
            UpdatedAt = now,
        };
        payment.Events.Add(NewEvent(RequestedEvent, PaymentStatus.Pending, null));
        order.Payments.Add(payment);
        database.Payments.Add(payment);
        await database.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task RefreshAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var order = await database.Orders
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Number == orderNumber, cancellationToken);
        if (order is null)
        {
            return;
        }

        foreach (var payment in order.Payments.Where(candidate => candidate.Status == PaymentStatus.Pending))
        {
            await ResolveAsync(order, payment, cancellationToken);
        }
    }

    public async Task<PaymentRetryOutcome> RetryAsync(
        string orderNumber, PaymentMethod method, string? phone, CancellationToken cancellationToken = default)
    {
        var normalized = PhoneNumber.Normalize(phone);
        if (normalized is null)
        {
            return PaymentRetryOutcome.InvalidPhone;
        }

        var order = await database.Orders
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Number == orderNumber, cancellationToken);
        if (order is null || !IsRetryable(order))
        {
            return PaymentRetryOutcome.NotRetryable;
        }

        await StartAsync(order, method, normalized, cancellationToken);
        await audit.WriteAsync(order.UserId, "payment.retry", OrderEntity, order.Number, $"New payment attempt for order {order.Number}", cancellationToken);
        return PaymentRetryOutcome.Started;
    }

    private static bool IsRetryable(Order order) =>
        order.Status == OrderStatus.AwaitingPayment
        && order.Payments.All(payment => payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Paid);

    private async Task ResolveAsync(Order order, Payment payment, CancellationToken cancellationToken)
    {
        var result = await gateway.GetStatusAsync(
            new PaymentStatusQuery(payment.ProviderReference ?? string.Empty, payment.Phone, payment.CreatedAt), cancellationToken);
        if (result.Status == PaymentStatus.Pending)
        {
            return;
        }

        var now = time.GetUtcNow();
        payment.Status = result.Status;
        payment.FailureReason = result.FailureReason;
        payment.UpdatedAt = now;
        payment.Events.Add(NewEvent(StatusEvent, result.Status, result.FailureReason));
        if (result.Status == PaymentStatus.Paid && order.Status == OrderStatus.AwaitingPayment)
        {
            order.Status = OrderStatus.Paid;
            order.UpdatedAt = now;
        }

        await database.SaveChangesAsync(cancellationToken);
        var action = result.Status == PaymentStatus.Paid ? "payment.paid" : "payment.failed";
        var summary = string.Create(CultureInfo.InvariantCulture, $"Payment {payment.Id} of order {order.Number} is {result.Status}");
        await audit.WriteAsync(null, action, OrderEntity, order.Number, summary, cancellationToken);
    }

    private PaymentEvent NewEvent(string kind, PaymentStatus status, string? reason) => new()
    {
        Kind = kind,
        RawPayload = JsonSerializer.Serialize(new { provider = gateway.Provider, status = status.ToString(), reason }),
        At = time.GetUtcNow(),
    };
}
