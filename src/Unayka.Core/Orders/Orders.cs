using System.Globalization;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Payments;

namespace Unayka.Core.Orders;

public enum OrderChannel
{
    Online,
    Counter,
}

public enum FulfillmentKind
{
    Pickup,
    Delivery,
}

public enum OrderStatus
{
    New,
    AwaitingPayment,
    Paid,
    Preparing,
    Ready,
    Delivered,
    Cancelled,
}

/// <summary>Customer order, online or at the counter. Amounts are in minor units.</summary>
public sealed class Order
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public OrderChannel Channel { get; set; }
    public FulfillmentKind Fulfillment { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Notes { get; set; }
    public OrderStatus Status { get; set; }
    public long SubtotalMinor { get; set; }
    public long DiscountMinor { get; set; }
    public long DeliveryFeeMinor { get; set; }
    public long TotalMinor { get; set; }
    public string? CouponCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}

/// <summary>Line of an order with the name, image, kind and price frozen at purchase time. LocationId is null for digital lines.</summary>
public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int? LocationId { get; set; }
    public ProductKind Kind { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public long UnitPriceMinor { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Right of a buyer to download a digital line, with a download counter for audit only.</summary>
public sealed class DigitalDownloadGrant
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public int UserId { get; set; }
    public int DownloadCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Checkout form data for placing an online order. Every order is collected at the pickup locations chosen in the cart.</summary>
public sealed record PlaceOrderRequest(
    Guid? CartId,
    int? UserId,
    string? Name,
    string? Phone,
    string? Email,
    string? Notes,
    PaymentMethod PaymentMethod);

public enum PlaceOrderOutcome
{
    Placed,
    EmptyCart,
    InvalidName,
    InvalidPhone,
    InvalidEmail,
    InvalidPaymentMethod,
    OutOfStock,
    CouponNoLongerValid,
    SignInRequired,
}

/// <summary>Placement result; CheckoutUrl is the provider page where the customer pays, when the provider returned one.</summary>
public sealed record PlaceOrderResult(PlaceOrderOutcome Outcome, string? OrderNumber, string? Detail, string? CheckoutUrl)
{
    public bool Succeeded => Outcome == PlaceOrderOutcome.Placed;
}

public static class OrderAccess
{
    /// <summary>An order is visible to staff, to its owner, and to the browser that placed it.</summary>
    public static bool CanView(OrderDetail order, int? viewerUserId, bool isStaff, IReadOnlyCollection<string> placedNumbers) =>
        isStaff
        || (viewerUserId is not null && order.UserId == viewerUserId)
        || placedNumbers.Contains(order.Number, StringComparer.Ordinal);
}

public static class OrderPayments
{
    /// <summary>True once the payment is confirmed and the order has not been cancelled.</summary>
    public static bool IsPaid(OrderStatus status) =>
        status is OrderStatus.Paid or OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Delivered;

    /// <summary>A digital line can be downloaded by the buyer of a paid order.</summary>
    public static bool CanDownload(OrderDetail order, OrderLineDetail line, int? viewerUserId) =>
        line.IsDigital && viewerUserId is not null && order.UserId == viewerUserId && IsPaid(order.Status);

    /// <summary>Same rule for an account list row.</summary>
    public static bool CanDownload(OrderListItem order, OrderLineDetail line) =>
        line.IsDigital && IsPaid(order.Status);
}

public interface IOrderNumberGenerator
{
    /// <summary>Returns the next order number in the format UN-000123.</summary>
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}

public interface IOrderPlacement
{
    /// <summary>Validates the checkout, recalculates totals, reserves stock and creates the order with a pending payment.</summary>
    Task<PlaceOrderResult> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Line of an order as shown to the customer, read from the snapshot taken at purchase time. ProductSlug is null when the product is no longer publicly visible.</summary>
public sealed record OrderLineDetail(
    int ItemId,
    string ProductName,
    string ImageUrl,
    long UnitPriceMinor,
    int Quantity,
    ProductKind Kind,
    string? ProductSlug,
    int? LocationId,
    string? LocationName,
    string? LocationAddress,
    string? LocationHours)
{
    public long LineTotalMinor => UnitPriceMinor * Quantity;

    public bool IsDigital => Kind == ProductKind.Digital;
}

/// <summary>Order summary row for the customer account list, with its lines.</summary>
public sealed record OrderListItem(
    string Number,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    long TotalMinor,
    int ItemCount,
    IReadOnlyList<OrderLineDetail> Lines);

/// <summary>Latest payment attempt of an order.</summary>
public sealed record OrderPaymentDetail(PaymentMethod Method, PaymentStatus Status, string? FailureReason, string? CheckoutUrl);

/// <summary>Everything the customer sees about one order.</summary>
public sealed record OrderDetail(
    string Number,
    int? UserId,
    OrderStatus Status,
    FulfillmentKind Fulfillment,
    string CustomerName,
    string CustomerPhone,
    string? DeliveryAddress,
    string? Notes,
    long SubtotalMinor,
    long DiscountMinor,
    long DeliveryFeeMinor,
    long TotalMinor,
    string? CouponCode,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineDetail> Lines,
    OrderPaymentDetail? Payment);

public interface IOrderQueries
{
    /// <summary>Lists the orders of a customer, newest first, with their lines.</summary>
    Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Loads one order by number, or null when it does not exist.</summary>
    Task<OrderDetail?> GetByNumberAsync(string number, CancellationToken cancellationToken = default);
}

public enum DownloadOutcome
{
    Granted,
    NotSignedIn,
    NotFound,
    NotPaid,
    FileMissing,
}

/// <summary>Result of a download request. File is set only when the outcome is Granted and the caller owns and must dispose it.</summary>
public sealed record DownloadResult(DownloadOutcome Outcome, DigitalFile? File);

/// <summary>Opened digital file ready to stream.</summary>
public sealed record DigitalFile(Stream Content, string FileName, string ContentType);

public interface IDigitalDownloads
{
    /// <summary>Opens the file of a digital order line for the signed in buyer of a paid order, counts the download and audits it.</summary>
    Task<DownloadResult> OpenAsync(int orderItemId, int? userId, CancellationToken cancellationToken = default);
}

public sealed class OrderNumberGenerator(UnaykaDbContext database) : IOrderNumberGenerator
{
    private const string Prefix = "UN-";
    private const string Format = "D6";

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var last = await database.Orders.AsNoTracking()
            .Where(order => order.Number.StartsWith(Prefix))
            .OrderByDescending(order => order.Number)
            .Select(order => order.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var lastValue = last is not null && int.TryParse(last.AsSpan(Prefix.Length), CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        return Prefix + (lastValue + 1).ToString(Format, CultureInfo.InvariantCulture);
    }
}

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

        var options = await payments.OnlineOptionsAsync(cancellationToken);
        if (options.All(option => option.Method != request.PaymentMethod))
        {
            return Fail(PlaceOrderOutcome.InvalidPaymentMethod);
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
        var payment = await payments.StartAsync(order, request.PaymentMethod, cancellationToken);
        await carts.ClearAsync(request.CartId!.Value, cancellationToken);
        await audit.WriteAsync(
            request.UserId, "order.placed", "Order", order.Number, $"Online order {order.Number} placed", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlaceOrderResult(PlaceOrderOutcome.Placed, order.Number, null, payment.CheckoutUrl);
    }

    private static PlaceOrderResult Fail(PlaceOrderOutcome outcome, string? detail = null) => new(outcome, null, detail, null);

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

        return null;
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

public sealed class OrderQueries(UnaykaDbContext database) : IOrderQueries
{
    public async Task<IReadOnlyList<OrderListItem>> ListForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var orders = await database.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
        var lines = await LoadLinesAsync(orders.SelectMany(order => order.Items).ToList(), cancellationToken);
        return orders
            .Select(order => new OrderListItem(
                order.Number,
                order.CreatedAt,
                order.Status,
                order.TotalMinor,
                order.Items.Sum(item => item.Quantity),
                order.Items.Select(item => lines[item.Id]).ToList()))
            .ToList();
    }

    public async Task<OrderDetail?> GetByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        var order = await database.Orders.AsNoTracking()
            .Include(candidate => candidate.Items)
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Number == number, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var lines = await LoadLinesAsync(order.Items, cancellationToken);
        var payment = order.Payments.OrderByDescending(candidate => candidate.Id).FirstOrDefault();
        return new OrderDetail(
            order.Number,
            order.UserId,
            order.Status,
            order.Fulfillment,
            order.CustomerName,
            order.CustomerPhone,
            order.DeliveryAddress,
            order.Notes,
            order.SubtotalMinor,
            order.DiscountMinor,
            order.DeliveryFeeMinor,
            order.TotalMinor,
            order.CouponCode,
            order.CreatedAt,
            order.Items.OrderBy(item => item.Id).Select(item => lines[item.Id]).ToList(),
            payment is null ? null : new OrderPaymentDetail(payment.Method, payment.Status, payment.FailureReason, payment.CheckoutUrl));
    }

    private async Task<Dictionary<int, OrderLineDetail>> LoadLinesAsync(IReadOnlyList<OrderItem> items, CancellationToken cancellationToken)
    {
        var productIds = items.Select(item => item.ProductId).Distinct().ToList();
        var locationIds = items.Where(item => item.LocationId is not null).Select(item => item.LocationId!.Value).Distinct().ToList();
        var slugs = await database.Products.AsNoTracking()
            .Where(ProductVisibility.IsPublic)
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, product => product.Slug, cancellationToken);
        var locations = await database.Locations.AsNoTracking()
            .Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, cancellationToken);
        return items.ToDictionary(
            item => item.Id,
            item =>
            {
                var location = item.LocationId is { } id && locations.TryGetValue(id, out var found) ? found : null;
                return new OrderLineDetail(
                    item.Id,
                    item.ProductName,
                    item.ImageUrl,
                    item.UnitPriceMinor,
                    item.Quantity,
                    item.Kind,
                    slugs.GetValueOrDefault(item.ProductId),
                    item.LocationId,
                    location?.Name,
                    location?.Address,
                    location?.OpeningHours);
            });
    }
}

public sealed class DigitalDownloads(UnaykaDbContext database, IDigitalFileStore files, IAuditLog audit, TimeProvider time) : IDigitalDownloads
{
    public async Task<DownloadResult> OpenAsync(int orderItemId, int? userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            return Refuse(DownloadOutcome.NotSignedIn);
        }

        var item = await database.OrderItems.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == orderItemId && candidate.Kind == ProductKind.Digital, cancellationToken);
        var order = item is null ? null : await database.Orders.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == item.OrderId, cancellationToken);
        if (item is null || order is null || order.UserId != userId)
        {
            return Refuse(DownloadOutcome.NotFound);
        }

        if (!OrderPayments.IsPaid(order.Status))
        {
            return Refuse(DownloadOutcome.NotPaid);
        }

        var asset = await database.DigitalAssets.AsNoTracking()
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefaultAsync(candidate => candidate.ProductId == item.ProductId, cancellationToken);
        var content = asset is null ? null : files.Open(asset.StorageKey);
        if (asset is null || content is null)
        {
            return Refuse(DownloadOutcome.FileMissing);
        }

        await CountAsync(item.Id, userId.Value, cancellationToken);
        await audit.WriteAsync(userId, "download.served", "OrderItem", item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Digital download of order {order.Number}", cancellationToken);
        return new DownloadResult(DownloadOutcome.Granted, new DigitalFile(content, asset.FileName, asset.ContentType));
    }

    private static DownloadResult Refuse(DownloadOutcome outcome) => new(outcome, null);

    private async Task CountAsync(int orderItemId, int userId, CancellationToken cancellationToken)
    {
        var grant = await database.DigitalDownloadGrants.FirstOrDefaultAsync(
            candidate => candidate.OrderItemId == orderItemId && candidate.UserId == userId, cancellationToken);
        if (grant is null)
        {
            grant = new DigitalDownloadGrant { OrderItemId = orderItemId, UserId = userId, CreatedAt = time.GetUtcNow() };
            database.DigitalDownloadGrants.Add(grant);
        }

        grant.DownloadCount++;
        await database.SaveChangesAsync(cancellationToken);
    }
}

public static class OrderServiceExtensions
{
    public static IServiceCollection AddUnaykaOrders(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddUnaykaPayments();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IOrderPlacement, OrderPlacement>();
        services.AddScoped<IOrderQueries, OrderQueries>();
        services.AddScoped<IDigitalDownloads, DigitalDownloads>();
        return services;
    }
}
