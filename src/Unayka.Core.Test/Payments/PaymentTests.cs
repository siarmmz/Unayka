using Microsoft.EntityFrameworkCore;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Test.Data;
using Unayka.Core.Test.Orders;

namespace Unayka.Core.Test.Payments;

public sealed class PaymentTests : IDisposable
{
    private const string GoodPhone = "841234567";
    private const string FailingPhone = "841234560";

    private readonly CheckoutFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, PaymentStatus.Pending)]
    [InlineData(4, PaymentStatus.Pending)]
    [InlineData(5, PaymentStatus.Paid)]
    [InlineData(60, PaymentStatus.Paid)]
    public async Task Mock_gateway_pays_after_five_seconds(int seconds, PaymentStatus expected)
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var gateway = new MockPaymentGateway(time);
        time.Advance(TimeSpan.FromSeconds(seconds));

        var result = await gateway.GetStatusAsync(new PaymentStatusQuery("ref", GoodPhone, DateTimeOffset.UnixEpoch));

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task Mock_gateway_fails_phones_ending_in_zero_with_a_reason()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var gateway = new MockPaymentGateway(time);
        time.Advance(TimeSpan.FromSeconds(10));

        var result = await gateway.GetStatusAsync(new PaymentStatusQuery("ref", FailingPhone, DateTimeOffset.UnixEpoch));

        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal("Saldo insuficiente", result.FailureReason);
    }

    [Fact]
    public async Task Refresh_marks_payment_and_order_paid_and_records_events()
    {
        var number = await PlaceOrderAsync(GoodPhone);

        await _fixture.Payments.RefreshAsync(number);
        var stillPending = await LoadOrderAsync(number);
        _fixture.Time.Advance(TimeSpan.FromSeconds(6));
        await _fixture.Payments.RefreshAsync(number);
        var paid = await LoadOrderAsync(number);

        Assert.Equal(OrderStatus.AwaitingPayment, stillPending.Status);
        Assert.Equal(OrderStatus.Paid, paid.Status);
        Assert.Equal(PaymentStatus.Paid, paid.Payments.Single().Status);
        Assert.Equal(2, await _fixture.Db.PaymentEvents.CountAsync(paymentEvent => paymentEvent.PaymentId == paid.Payments.Single().Id));
    }

    [Fact]
    public async Task Failed_payment_keeps_the_order_waiting_and_allows_a_retry_that_succeeds()
    {
        var number = await PlaceOrderAsync(FailingPhone);
        _fixture.Time.Advance(TimeSpan.FromSeconds(4));
        await _fixture.Payments.RefreshAsync(number);

        var invalid = await _fixture.Payments.RetryAsync(number, PaymentMethod.MPesa, "12");
        var retry = await _fixture.Payments.RetryAsync(number, PaymentMethod.EMola, GoodPhone);
        _fixture.Time.Advance(TimeSpan.FromSeconds(6));
        await _fixture.Payments.RefreshAsync(number);
        var order = await LoadOrderAsync(number);

        Assert.Equal(PaymentRetryOutcome.InvalidPhone, invalid);
        Assert.Equal(PaymentRetryOutcome.Started, retry);
        Assert.Equal(2, order.Payments.Count);
        Assert.Contains(order.Payments, payment => payment is { Status: PaymentStatus.Failed, FailureReason: "Saldo insuficiente" });
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task Retry_is_refused_while_a_payment_is_pending()
    {
        var number = await PlaceOrderAsync(GoodPhone);

        var outcome = await _fixture.Payments.RetryAsync(number, PaymentMethod.MPesa, GoodPhone);

        Assert.Equal(PaymentRetryOutcome.NotRetryable, outcome);
    }

    private async Task<string> PlaceOrderAsync(string walletPhone)
    {
        var product = await _fixture.FirstProductAsync();
        var cart = await _fixture.Carts.AddAsync(null, null, product.Id, 1);
        var result = await _fixture.Placement.PlaceAsync(new PlaceOrderRequest(
            cart.CartId, null, "Ana", GoodPhone, null, FulfillmentKind.Pickup, null, null, PaymentMethod.MPesa, walletPhone));
        Assert.True(result.Succeeded);
        return result.OrderNumber!;
    }

    private Task<Order> LoadOrderAsync(string number)
    {
        _fixture.Db.ChangeTracker.Clear();
        return _fixture.Db.Orders.AsNoTracking().Include(order => order.Payments).SingleAsync(order => order.Number == number);
    }
}
