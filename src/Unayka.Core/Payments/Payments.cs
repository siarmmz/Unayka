using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unayka.Core.Audit;
using Unayka.Core.Configuration;
using Unayka.Core.Data;
using Unayka.Core.Orders;

namespace Unayka.Core.Payments;

public enum PaymentMethod
{
    MPesa,
    EMola,
    Cash,
    Card,
    MKesh,
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Cancelled,
}

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
    public string? CheckoutUrl { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<PaymentEvent> Events { get; set; } = [];
}

/// <summary>Raw notification or state change received for a payment.</summary>
public sealed class PaymentEvent
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }
}

/// <summary>Payment method offered online or at the counter, with its label, logo, icon and provider code, managed in the dashboard.</summary>
public sealed class PaymentOption
{
    public int Id { get; set; }
    public PaymentMethod Method { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? ProviderCode { get; set; }
    public bool IsOnline { get; set; }
    public bool IsCounter { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Payment option as shown to customers and cashiers.</summary>
public sealed record PaymentOptionView(PaymentMethod Method, string Label, IReadOnlyList<string> LogoUrls, string Icon);

/// <summary>Reads the space separated logo URLs stored on a payment option.</summary>
public static class PaymentLogos
{
    public static IReadOnlyList<string> Split(string? logoUrl) =>
        string.IsNullOrWhiteSpace(logoUrl) ? [] : logoUrl.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Payment request sent to the provider. Reference is unique per attempt; ReturnUrl is where the customer comes back to.</summary>
public sealed record PaymentRequest(string Reference, long AmountMinor, string Description, string? ProviderCode, string? ReturnUrl);

/// <summary>Provider acknowledgement with the page where the customer completes the payment.</summary>
public sealed record PaymentRequestResult(string ProviderReference, string? CheckoutUrl);

/// <summary>Status reported by the provider, with the reason when the payment failed.</summary>
public sealed record PaymentStatusResult(PaymentStatus Status, string? FailureReason);

/// <summary>Raised when the provider is not configured, unreachable or rejects the request; Message is safe to show to customers.</summary>
public sealed class PaymentGatewayException(string message) : Exception(message);

public interface IPaymentMethod
{
    /// <summary>Method handled by this implementation.</summary>
    PaymentMethod Method { get; }

    /// <summary>Short provider name stored on each payment.</summary>
    string Provider { get; }

    /// <summary>Asks the provider to collect the amount and returns its reference and checkout page.</summary>
    Task<PaymentRequestResult> RequestAsync(PaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads the current status of a payment previously requested.</summary>
    Task<PaymentStatusResult> GetStatusAsync(string providerReference, CancellationToken cancellationToken = default);
}

/// <summary>Payment status change carried by a verified provider notification.</summary>
public sealed record PaymentNotification(string ProviderReference, PaymentStatus Status, string EventName);

public interface IPaySuiteClient
{
    /// <summary>Creates a PaySuite payment request for the given method code.</summary>
    Task<PaymentRequestResult> CreatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads a PaySuite payment request by id.</summary>
    Task<PaymentStatusResult> GetPaymentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Verifies the X-Signature of a webhook body and reads it, or returns null when the signature or body is invalid.</summary>
    PaymentNotification? ReadWebhook(string body, string? signature);
}

public enum PaymentRetryOutcome
{
    Started,
    Failed,
    NotRetryable,
}

/// <summary>Result of a retry, with the checkout page when the provider accepted the new attempt.</summary>
public sealed record PaymentRetryResult(PaymentRetryOutcome Outcome, string? CheckoutUrl, string? FailureReason);

public enum WebhookOutcome
{
    Applied,
    Ignored,
    InvalidSignature,
}

public interface IPaymentService
{
    /// <summary>Active payment options for online checkout, in display order.</summary>
    Task<IReadOnlyList<PaymentOptionView>> OnlineOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Active payment options for the counter, in display order.</summary>
    Task<IReadOnlyList<PaymentOptionView>> CounterOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Requests the payment from the provider and attaches it to the order; a provider error leaves a failed payment with the reason.</summary>
    Task<Payment> StartAsync(Order order, PaymentMethod method, CancellationToken cancellationToken = default);

    /// <summary>Resolves pending payments of the order with the provider and marks the order paid when confirmed.</summary>
    Task RefreshAsync(string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>Creates a new payment for an order whose last payment failed.</summary>
    Task<PaymentRetryResult> RetryAsync(string orderNumber, PaymentMethod method, CancellationToken cancellationToken = default);

    /// <summary>Applies a PaySuite webhook after verifying its signature.</summary>
    Task<WebhookOutcome> HandlePaySuiteWebhookAsync(string body, string? signature, CancellationToken cancellationToken = default);
}

public static class WalletMethods
{
    public static readonly PaymentMethod[] All = [PaymentMethod.MPesa, PaymentMethod.EMola, PaymentMethod.MKesh];

    public static bool IsWallet(this PaymentMethod method) => All.Contains(method);
}

public sealed class PaymentService(
    UnaykaDbContext database,
    IEnumerable<IPaymentMethod> methods,
    IPaySuiteClient paySuite,
    IAuditLog audit,
    TimeProvider time) : IPaymentService
{
    public const string UnavailableMethodReason = "Este meio de pagamento não está disponível de momento.";
    private const string OrderEntity = "Order";
    private const string RequestedEvent = "requested";
    private const string FailedEvent = "request-failed";
    private const string StatusEvent = "status";
    private const string OrderPath = "/pedido/";
    private const string DescriptionPrefix = "Unayka, pedido ";

    public Task<IReadOnlyList<PaymentOptionView>> OnlineOptionsAsync(CancellationToken cancellationToken = default) =>
        OptionsAsync(option => option.IsOnline, cancellationToken);

    public Task<IReadOnlyList<PaymentOptionView>> CounterOptionsAsync(CancellationToken cancellationToken = default) =>
        OptionsAsync(option => option.IsCounter, cancellationToken);

    public async Task<Payment> StartAsync(Order order, PaymentMethod method, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var handler = methods.FirstOrDefault(candidate => candidate.Method == method);
        var payment = new Payment
        {
            Order = order,
            Provider = handler?.Provider ?? string.Empty,
            Method = method,
            AmountMinor = order.TotalMinor,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        try
        {
            var option = await database.PaymentOptions.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Method == method && candidate.IsActive && candidate.IsOnline, cancellationToken);
            if (option is null || handler is null)
            {
                throw new PaymentGatewayException(UnavailableMethodReason);
            }

            var reference = $"{order.Number}-{order.Payments.Count + 1}";
            var description = DescriptionPrefix + order.Number;
            var requested = await handler.RequestAsync(
                new PaymentRequest(reference, order.TotalMinor, description, option.ProviderCode, ReturnUrl(order.Number)), cancellationToken);
            payment.ProviderReference = requested.ProviderReference;
            payment.CheckoutUrl = requested.CheckoutUrl;
            payment.Events.Add(NewEvent(RequestedEvent, payment.Provider, PaymentStatus.Pending, null));
        }
        catch (PaymentGatewayException exception)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = exception.Message;
            payment.Events.Add(NewEvent(FailedEvent, payment.Provider, PaymentStatus.Failed, exception.Message));
        }

        order.Payments.Add(payment);
        database.Payments.Add(payment);
        await database.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task RefreshAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderNumber, cancellationToken);
        if (order is null)
        {
            return;
        }

        foreach (var payment in order.Payments.Where(candidate => candidate.Status == PaymentStatus.Pending && candidate.ProviderReference is not null))
        {
            var handler = methods.FirstOrDefault(candidate => candidate.Method == payment.Method && candidate.Provider == payment.Provider);
            if (handler is null)
            {
                continue;
            }

            try
            {
                var result = await handler.GetStatusAsync(payment.ProviderReference!, cancellationToken);
                await ApplyAsync(order, payment, result.Status, result.FailureReason, StatusEvent, cancellationToken);
            }
            catch (PaymentGatewayException)
            {
                return;
            }
        }
    }

    public async Task<PaymentRetryResult> RetryAsync(string orderNumber, PaymentMethod method, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderNumber, cancellationToken);
        if (order is null || !IsRetryable(order))
        {
            return new PaymentRetryResult(PaymentRetryOutcome.NotRetryable, null, null);
        }

        var payment = await StartAsync(order, method, cancellationToken);
        await audit.WriteAsync(order.UserId, "payment.retry", OrderEntity, order.Number, $"New payment attempt for order {order.Number}", cancellationToken);
        return payment.Status == PaymentStatus.Failed
            ? new PaymentRetryResult(PaymentRetryOutcome.Failed, null, payment.FailureReason)
            : new PaymentRetryResult(PaymentRetryOutcome.Started, payment.CheckoutUrl, null);
    }

    public async Task<WebhookOutcome> HandlePaySuiteWebhookAsync(string body, string? signature, CancellationToken cancellationToken = default)
    {
        var notification = paySuite.ReadWebhook(body, signature);
        if (notification is null)
        {
            return WebhookOutcome.InvalidSignature;
        }

        var payment = await database.Payments
            .Include(candidate => candidate.Order).ThenInclude(order => order!.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Provider == PaySuiteClient.ProviderName && candidate.ProviderReference == notification.ProviderReference, cancellationToken);
        if (payment?.Order is null || payment.Status != PaymentStatus.Pending)
        {
            return WebhookOutcome.Ignored;
        }

        payment.Events.Add(new PaymentEvent { Kind = notification.EventName, RawPayload = body, At = time.GetUtcNow() });
        await ApplyAsync(payment.Order, payment, notification.Status, null, notification.EventName, cancellationToken);
        return WebhookOutcome.Applied;
    }

    private async Task<IReadOnlyList<PaymentOptionView>> OptionsAsync(
        System.Linq.Expressions.Expression<Func<PaymentOption, bool>> channel, CancellationToken cancellationToken)
    {
        var options = await database.PaymentOptions.AsNoTracking()
            .Where(option => option.IsActive)
            .Where(channel)
            .OrderBy(option => option.SortOrder)
            .ToListAsync(cancellationToken);
        return options.Select(option => new PaymentOptionView(option.Method, option.Label, PaymentLogos.Split(option.LogoUrl), option.Icon)).ToList();
    }

    private Task<Order?> LoadOrderAsync(string orderNumber, CancellationToken cancellationToken) =>
        database.Orders
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Number == orderNumber, cancellationToken);

    private static string? ReturnUrl(string orderNumber) =>
        string.IsNullOrWhiteSpace(AppEnvConfig.PublicBaseUrl) ? null : AppEnvConfig.PublicBaseUrl.TrimEnd('/') + OrderPath + orderNumber;

    private static bool IsRetryable(Order order) =>
        order.Status == OrderStatus.AwaitingPayment
        && order.Payments.All(payment => payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Paid);

    private async Task ApplyAsync(Order order, Payment payment, PaymentStatus status, string? reason, string eventKind, CancellationToken cancellationToken)
    {
        if (status == PaymentStatus.Pending)
        {
            return;
        }

        var now = time.GetUtcNow();
        payment.Status = status;
        payment.FailureReason = reason;
        payment.UpdatedAt = now;
        payment.Events.Add(NewEvent(eventKind, payment.Provider, status, reason));
        if (status == PaymentStatus.Paid && order.Status == OrderStatus.AwaitingPayment)
        {
            order.Status = OrderStatus.Paid;
            order.UpdatedAt = now;
        }

        await database.SaveChangesAsync(cancellationToken);
        var action = status == PaymentStatus.Paid ? "payment.paid" : "payment.failed";
        var summary = string.Create(CultureInfo.InvariantCulture, $"Payment {payment.Id} of order {order.Number} is {status}");
        await audit.WriteAsync(null, action, OrderEntity, order.Number, summary, cancellationToken);
    }

    private PaymentEvent NewEvent(string kind, string provider, PaymentStatus status, string? reason) => new()
    {
        Kind = kind,
        RawPayload = JsonSerializer.Serialize(new { provider, status = status.ToString(), reason }),
        At = time.GetUtcNow(),
    };
}

public sealed class PaySuitePaymentMethod(PaymentMethod method, IPaySuiteClient client) : IPaymentMethod
{
    public PaymentMethod Method => method;

    public string Provider => PaySuiteClient.ProviderName;

    public Task<PaymentRequestResult> RequestAsync(PaymentRequest request, CancellationToken cancellationToken = default) =>
        client.CreatePaymentAsync(request, cancellationToken);

    public Task<PaymentStatusResult> GetStatusAsync(string providerReference, CancellationToken cancellationToken = default) =>
        client.GetPaymentAsync(providerReference, cancellationToken);
}

public sealed class PaySuiteClient(HttpClient http) : IPaySuiteClient
{
    public const string ProviderName = "paysuite";
    public const string UnavailableReason = "O serviço de pagamento está indisponível. Tente novamente mais tarde.";
    public const string RejectedReason = "O serviço de pagamento recusou o pedido. Tente outro meio de pagamento.";
    private const string PaymentsPath = "payments";
    private const string PaidStatus = "paid";
    private const string FailedStatus = "failed";
    private const string SuccessEvent = "payment.success";
    private const string FailedEvent = "payment.failed";
    private const int MinorUnitsPerMzn = 100;

    public async Task<PaymentRequestResult> CreatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, string?>
        {
            ["amount"] = (request.AmountMinor / (decimal)MinorUnitsPerMzn).ToString("0.00", CultureInfo.InvariantCulture),
            ["reference"] = request.Reference,
            ["description"] = request.Description,
            ["method"] = request.ProviderCode,
            ["return_url"] = request.ReturnUrl,
        }.Where(pair => pair.Value is not null).ToDictionary();
        using var message = NewMessage(HttpMethod.Post, PaymentsPath);
        message.Content = JsonContent.Create(body);
        var data = await SendAsync(message, cancellationToken);
        var id = data.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
        var checkoutUrl = data.TryGetProperty("checkout_url", out var urlValue) ? urlValue.GetString() : null;
        return string.IsNullOrEmpty(id) ? throw new PaymentGatewayException(UnavailableReason) : new PaymentRequestResult(id, checkoutUrl);
    }

    public async Task<PaymentStatusResult> GetPaymentAsync(string id, CancellationToken cancellationToken = default)
    {
        using var message = NewMessage(HttpMethod.Get, $"{PaymentsPath}/{Uri.EscapeDataString(id)}");
        var data = await SendAsync(message, cancellationToken);
        var status = data.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
        return status switch
        {
            PaidStatus => new PaymentStatusResult(PaymentStatus.Paid, null),
            FailedStatus => new PaymentStatusResult(PaymentStatus.Failed, RejectedReason),
            _ => new PaymentStatusResult(PaymentStatus.Pending, null),
        };
    }

    public PaymentNotification? ReadWebhook(string body, string? signature)
    {
        var secret = AppEnvConfig.PaySuiteWebhookSecret;
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signature) || !SignatureMatches(body, signature, secret))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var eventName = root.GetProperty("event").GetString();
            var id = root.GetProperty("data").GetProperty("id").GetString();
            var status = eventName switch
            {
                SuccessEvent => PaymentStatus.Paid,
                FailedEvent => PaymentStatus.Failed,
                _ => PaymentStatus.Pending,
            };
            return eventName is null || string.IsNullOrEmpty(id) ? null : new PaymentNotification(id, status, eventName);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static bool SignatureMatches(string body, string signature, string secret)
    {
        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    private static HttpRequestMessage NewMessage(HttpMethod method, string path)
    {
        var baseUrl = AppEnvConfig.PaySuiteBaseUrl;
        var apiKey = AppEnvConfig.PaySuiteApiKey;
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new PaymentGatewayException(UnavailableReason);
        }

        var message = new HttpRequestMessage(method, new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return message;
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new PaymentGatewayException((int)response.StatusCode is >= 400 and < 500 ? RejectedReason : UnavailableReason);
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("data", out var data) ? data.Clone() : throw new PaymentGatewayException(UnavailableReason);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new PaymentGatewayException(UnavailableReason);
        }
    }
}

public static class PaymentServiceExtensions
{
    private static readonly PaymentMethod[] PaySuiteMethods = [PaymentMethod.MPesa, PaymentMethod.EMola, PaymentMethod.Card];

    public static IServiceCollection AddUnaykaPayments(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IPaySuiteClient, PaySuiteClient>();
        foreach (var method in PaySuiteMethods)
        {
            services.AddScoped<IPaymentMethod>(provider => new PaySuitePaymentMethod(method, provider.GetRequiredService<IPaySuiteClient>()));
        }

        services.AddScoped<IPaymentService, PaymentService>();
        return services;
    }
}
