using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Carts;
using Unayka.Core.Data;
using Unayka.Core.Payments;

namespace Unayka.Core.Orders;

public sealed class OrderPlacement(
    UnaykaDbContext database,
    ICartService carts,
    IOrderNumberGenerator numbers,
    IPaymentService payments,
    IAuditLog audit,
    TimeProvider time) : IOrderPlacement
{
    private const int MaxNameLength = 80;
    private const int MaxEmailLength = 120;
    private const int MaxAddressLength = 300;
    private const int MaxNotesLength = 500;

    public async Task<PlaceOrderResult> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Fail(invalid.Value);
        }

        var cart = await carts.GetAsync(request.CartId, cancellationToken);
        if (cart.IsEmpty)
        {
            return Fail(PlaceOrderOutcome.EmptyCart);
        }

        if (cart.CouponRejection is not null)
        {
            return Fail(PlaceOrderOutcome.CouponNoLongerValid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var shortage = await ReserveStockAsync(cart, cancellationToken);
        if (shortage is not null)
        {
            return Fail(PlaceOrderOutcome.OutOfStock, shortage);
        }

        var order = await CreateOrderAsync(request, cart, cancellationToken);
        await payments.StartAsync(order, request.PaymentMethod, PhoneNumber.Normalize(request.WalletPhone)!, cancellationToken);
        await carts.ClearAsync(request.CartId!.Value, cancellationToken);
        await audit.WriteAsync(
            request.UserId, "order.placed", "Order", order.Number, $"Online order {order.Number} placed", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlaceOrderResult(PlaceOrderOutcome.Placed, order.Number, null);
    }

    private static PlaceOrderResult Fail(PlaceOrderOutcome outcome, string? detail = null) => new(outcome, null, detail);

    private static PlaceOrderOutcome? Validate(PlaceOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > MaxNameLength)
        {
            return PlaceOrderOutcome.InvalidName;
        }

        if (PhoneNumber.Normalize(request.Phone) is null)
        {
            return PlaceOrderOutcome.InvalidPhone;
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
        {
            return PlaceOrderOutcome.InvalidEmail;
        }

        if (request.Fulfillment == FulfillmentKind.Delivery && string.IsNullOrWhiteSpace(request.Address))
        {
            return PlaceOrderOutcome.AddressRequired;
        }

        if (request.PaymentMethod is not (PaymentMethod.MPesa or PaymentMethod.EMola))
        {
            return PlaceOrderOutcome.InvalidPaymentMethod;
        }

        return PhoneNumber.Normalize(request.WalletPhone) is null ? PlaceOrderOutcome.InvalidWalletPhone : null;
    }

    private static bool IsValidEmail(string value) =>
        value.Trim().Length <= MaxEmailLength && MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();

    private async Task<string?> ReserveStockAsync(CartView cart, CancellationToken cancellationToken)
    {
        var ids = cart.Lines.Select(line => line.ProductId).ToList();
        var products = await database.Products.Where(product => ids.Contains(product.Id)).ToListAsync(cancellationToken);
        var unavailable = cart.Lines.FirstOrDefault(line =>
            products.FirstOrDefault(candidate => candidate.Id == line.ProductId) is not { IsActive: true } product
            || product.StockQuantity < line.Quantity);
        if (unavailable is not null)
        {
            return unavailable.Name;
        }

        foreach (var line in cart.Lines)
        {
            var product = products.First(candidate => candidate.Id == line.ProductId);
            product.StockQuantity -= line.Quantity;
            product.SalesCount += line.Quantity;
        }

        return null;
    }

    private async Task<Order> CreateOrderAsync(PlaceOrderRequest request, CartView cart, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var deliveryFee = OrderPricing.DeliveryFor(request.Fulfillment);
        var order = new Order
        {
            Number = await numbers.NextAsync(cancellationToken),
            UserId = request.UserId,
            CustomerName = request.Name!.Trim(),
            CustomerPhone = PhoneNumber.Normalize(request.Phone)!,
            CustomerEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Channel = OrderChannel.Online,
            Fulfillment = request.Fulfillment,
            DeliveryAddress = request.Fulfillment == FulfillmentKind.Delivery ? Truncate(request.Address, MaxAddressLength) : null,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : Truncate(request.Notes, MaxNotesLength),
            Status = OrderStatus.AwaitingPayment,
            SubtotalMinor = cart.SubtotalMinor,
            DiscountMinor = cart.DiscountMinor,
            DeliveryFeeMinor = deliveryFee,
            TotalMinor = cart.TotalMinor + deliveryFee,
            CouponCode = cart.CouponCode,
            CreatedAt = now,
            UpdatedAt = now,
            Items = cart.Lines
                .Select(line => new OrderItem
                {
                    ProductId = line.ProductId,
                    ProductName = line.Name,
                    UnitPriceMinor = line.UnitPriceMinor,
                    Quantity = line.Quantity,
                })
                .ToList(),
        };
        database.Orders.Add(order);
        await RegisterCouponUseAsync(cart.CouponCode, cancellationToken);
        return order;
    }

    private async Task RegisterCouponUseAsync(string? code, CancellationToken cancellationToken)
    {
        if (code is null)
        {
            return;
        }

        var coupon = await database.Coupons.FirstOrDefaultAsync(candidate => candidate.Code == code, cancellationToken);
        if (coupon is not null)
        {
            coupon.UsedCount++;
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
