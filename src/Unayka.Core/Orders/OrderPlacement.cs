using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;
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

        if (cart.HasDigital && request.UserId is null)
        {
            return Fail(PlaceOrderOutcome.SignInRequired);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var reservation = await ReserveStockAsync(cart, cancellationToken);
        if (reservation.Shortage is not null)
        {
            return Fail(PlaceOrderOutcome.OutOfStock, reservation.Shortage);
        }

        var order = await CreateOrderAsync(request, cart, reservation.Items, cancellationToken);
        await payments.StartAsync(order, request.PaymentMethod, request.PaymentMethod.IsWallet() ? PhoneNumber.Normalize(request.WalletPhone) : null, cancellationToken);
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

        if (!request.PaymentMethod.IsOnline())
        {
            return PlaceOrderOutcome.InvalidPaymentMethod;
        }

        return request.PaymentMethod.IsWallet() && PhoneNumber.Normalize(request.WalletPhone) is null ? PlaceOrderOutcome.InvalidWalletPhone : null;
    }

    private static bool IsValidEmail(string value) =>
        value.Trim().Length <= MaxEmailLength && MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();

    private async Task<Reservation> ReserveStockAsync(CartView cart, CancellationToken cancellationToken)
    {
        var ids = cart.Lines.Select(line => line.ProductId).ToList();
        var products = await database.Products
            .Include(product => product.Stocks).ThenInclude(stock => stock.Location)
            .Where(ProductVisibility.IsPublic)
            .Where(product => ids.Contains(product.Id))
            .ToListAsync(cancellationToken);
        var shortage = cart.Lines.FirstOrDefault(line => !CanReserve(line, products.FirstOrDefault(candidate => candidate.Id == line.ProductId)));
        if (shortage is not null)
        {
            return new Reservation([], ShortageLabel(shortage));
        }

        var items = cart.Lines.Select(line => Reserve(line, products.First(candidate => candidate.Id == line.ProductId))).ToList();
        return new Reservation(items, null);
    }

    private static bool CanReserve(CartLineView line, Product? product)
    {
        if (product is null)
        {
            return false;
        }

        return product.Kind == ProductKind.Digital || StockAtChosenLocation(product, line.LocationId)?.Quantity >= line.Quantity;
    }

    private static string ShortageLabel(CartLineView line) =>
        line.IsDigital || line.LocationName is null ? line.Name : $"{line.Name} ({line.LocationName})";

    private static OrderItem Reserve(CartLineView line, Product product)
    {
        var stock = product.Kind == ProductKind.Digital ? null : StockAtChosenLocation(product, line.LocationId);
        product.SalesCount += line.Quantity;
        if (stock is not null)
        {
            stock.Quantity -= line.Quantity;
        }

        return new OrderItem
        {
            ProductId = product.Id,
            LocationId = stock?.LocationId,
            Kind = product.Kind,
            ProductName = line.Name,
            ImageUrl = line.ImageUrl,
            UnitPriceMinor = line.UnitPriceMinor,
            Quantity = line.Quantity,
        };
    }

    private static LocationStock? StockAtChosenLocation(Product product, int? locationId) =>
        product.Stocks.FirstOrDefault(stock => stock.LocationId == locationId && stock.Location?.Status == CatalogStatus.Active);

    private sealed record Reservation(IReadOnlyList<OrderItem> Items, string? Shortage);

    private async Task<Order> CreateOrderAsync(PlaceOrderRequest request, CartView cart, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var order = new Order
        {
            Number = await numbers.NextAsync(cancellationToken),
            UserId = request.UserId,
            CustomerName = request.Name!.Trim(),
            CustomerPhone = PhoneNumber.Normalize(request.Phone)!,
            CustomerEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Channel = OrderChannel.Online,
            Fulfillment = FulfillmentKind.Pickup,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : Truncate(request.Notes, MaxNotesLength),
            Status = OrderStatus.AwaitingPayment,
            SubtotalMinor = cart.SubtotalMinor,
            DiscountMinor = cart.DiscountMinor,
            DeliveryFeeMinor = 0,
            TotalMinor = cart.TotalMinor,
            CouponCode = cart.CouponCode,
            CreatedAt = now,
            UpdatedAt = now,
            Items = items.ToList(),
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
