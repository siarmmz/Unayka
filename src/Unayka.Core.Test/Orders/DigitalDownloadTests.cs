using Microsoft.EntityFrameworkCore;
using Unayka.Core.Orders;
using Unayka.Core.Orders.Downloads;
using Unayka.Core.Payments;

namespace Unayka.Core.Test.Orders;

public sealed class DigitalDownloadTests : IDisposable
{
    private const string Phone = "841234567";
    private const int Buyer = 7;
    private const int Stranger = 8;

    private readonly CheckoutFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task The_buyer_of_a_paid_order_downloads_and_each_download_is_counted_and_audited()
    {
        var itemId = await PlaceDigitalOrderAsync(OrderStatus.Paid);

        var first = await _fixture.Downloads.OpenAsync(itemId, Buyer);
        var second = await _fixture.Downloads.OpenAsync(itemId, Buyer);

        Assert.Equal(DownloadOutcome.Granted, first.Outcome);
        Assert.Equal(DownloadOutcome.Granted, second.Outcome);
        Assert.NotNull(first.File);
        await first.File.Content.DisposeAsync();
        await second.File!.Content.DisposeAsync();
        var grant = await _fixture.Db.DigitalDownloadGrants.SingleAsync();
        Assert.Equal(2, grant.DownloadCount);
        Assert.Equal(Buyer, grant.UserId);
        Assert.Equal(2, await _fixture.Db.AuditEntries.CountAsync(entry => entry.Action == "download.served"));
    }

    [Fact]
    public async Task Another_user_is_refused()
    {
        var itemId = await PlaceDigitalOrderAsync(OrderStatus.Paid);

        var result = await _fixture.Downloads.OpenAsync(itemId, Stranger);

        Assert.Equal(DownloadOutcome.NotFound, result.Outcome);
        Assert.Null(result.File);
        Assert.Empty(_fixture.Db.DigitalDownloadGrants);
    }

    [Fact]
    public async Task An_anonymous_visitor_is_refused()
    {
        var itemId = await PlaceDigitalOrderAsync(OrderStatus.Paid);

        var result = await _fixture.Downloads.OpenAsync(itemId, null);

        Assert.Equal(DownloadOutcome.NotSignedIn, result.Outcome);
        Assert.Null(result.File);
    }

    [Fact]
    public async Task An_order_that_is_not_paid_yet_is_refused()
    {
        var itemId = await PlaceDigitalOrderAsync(OrderStatus.AwaitingPayment);

        var result = await _fixture.Downloads.OpenAsync(itemId, Buyer);

        Assert.Equal(DownloadOutcome.NotPaid, result.Outcome);
    }

    [Fact]
    public async Task A_physical_line_is_not_downloadable()
    {
        var product = (await _fixture.PhysicalProductsAsync(1))[0];
        var cart = await _fixture.Carts.AddAsync(null, Buyer, product.Id, 1);
        var placed = await _fixture.Placement.PlaceAsync(Request(cart.CartId));
        var order = await _fixture.Db.Orders.Include(candidate => candidate.Items).SingleAsync(candidate => candidate.Number == placed.OrderNumber);
        order.Status = OrderStatus.Paid;
        await _fixture.Db.SaveChangesAsync();

        var result = await _fixture.Downloads.OpenAsync(order.Items.Single().Id, Buyer);

        Assert.Equal(DownloadOutcome.NotFound, result.Outcome);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.AwaitingPayment, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Only_paid_orders_allow_downloads(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderPayments.IsPaid(status));
    }

    private async Task<int> PlaceDigitalOrderAsync(OrderStatus status)
    {
        var ebook = await _fixture.EbookAsync();
        var cart = await _fixture.Carts.AddAsync(null, Buyer, ebook.Id, 1);
        var placed = await _fixture.Placement.PlaceAsync(Request(cart.CartId));
        var order = await _fixture.Db.Orders.Include(candidate => candidate.Items).SingleAsync(candidate => candidate.Number == placed.OrderNumber);
        order.Status = status;
        await _fixture.Db.SaveChangesAsync();
        return order.Items.Single().Id;
    }

    private static PlaceOrderRequest Request(Guid? cartId) =>
        new(cartId, Buyer, "Ana Silva", Phone, null, null, PaymentMethod.MPesa, Phone);
}
