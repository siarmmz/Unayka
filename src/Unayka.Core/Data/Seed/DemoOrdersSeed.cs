using Microsoft.EntityFrameworkCore;
using Unayka.Core.Catalog;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Data.Seed;

public static class DemoOrdersSeed
{
    private const int OrderCount = 20;
    private const int OrderUpdateMinutes = 30;
    private const int HistoryDays = 29;
    private const int OrdersPerCycle = 10;
    private const int PercentBase = 100;
    private const long DeliveryFeeMinor = 15000;
    private const string WelcomeCoupon = "BEMVINDO10";
    private const string MockProvider = "mock";
    private const string CounterProvider = "counter";
    private const string CounterCustomerName = "Cliente balcão";
    private const string InsufficientBalanceReason = "Saldo insuficiente";
    private const int MobileFeePercent = 3;
    private const int CouponEvery = 5;
    private const int DeliveryEvery = 3;
    private const int FirstProductStep = 5;
    private const int SecondProductStep = 7;
    private const int SecondProductOffset = 3;
    private const long WelcomeDiscountBasisPoints = 1000;
    private const long BasisPointsBase = 10000;

    public static readonly IReadOnlyList<(string Name, string Phone)> Customers =
    [
        ("Amélia Macuácua", "841234567"),
        ("Carlos Nhaca", "842345678"),
        ("Isabel Mondlane", "843456789"),
    ];

    private static readonly (OrderStatus Order, PaymentStatus Payment, PaymentMethod Method)[] Cycle =
    [
        (OrderStatus.Delivered, PaymentStatus.Paid, PaymentMethod.MPesa),
        (OrderStatus.Delivered, PaymentStatus.Paid, PaymentMethod.EMola),
        (OrderStatus.Delivered, PaymentStatus.Paid, PaymentMethod.Cash),
        (OrderStatus.Paid, PaymentStatus.Paid, PaymentMethod.MPesa),
        (OrderStatus.Cancelled, PaymentStatus.Cancelled, PaymentMethod.EMola),
        (OrderStatus.Delivered, PaymentStatus.Paid, PaymentMethod.MPesa),
        (OrderStatus.Preparing, PaymentStatus.Paid, PaymentMethod.EMola),
        (OrderStatus.AwaitingPayment, PaymentStatus.Pending, PaymentMethod.MPesa),
        (OrderStatus.Delivered, PaymentStatus.Paid, PaymentMethod.Cash),
        (OrderStatus.Cancelled, PaymentStatus.Failed, PaymentMethod.MPesa),
    ];

    public static async Task SeedAsync(UnaykaDbContext database, DateTimeOffset now)
    {
        if (await database.Orders.AnyAsync())
        {
            return;
        }

        var products = await database.Products.Include(product => product.Stocks).Include(product => product.Media).OrderBy(product => product.Id).ToListAsync();
        var customers = await database.Users
            .Where(user => user.Role == Accounts.UserRole.Customer && user.Phone != null)
            .OrderBy(user => user.Id)
            .ToListAsync();
        if (products.Count == 0 || customers.Count == 0)
        {
            return;
        }

        var orders = Enumerable.Range(0, OrderCount)
            .Select(index => CreateOrder(index, products, customers, now))
            .ToList();
        database.Orders.AddRange(orders);

        var welcome = await database.Coupons.SingleAsync(coupon => coupon.Code == WelcomeCoupon);
        welcome.UsedCount += orders.Count(order => order.CouponCode == WelcomeCoupon);
        await database.SaveChangesAsync();
    }

    private static Order CreateOrder(int index, List<Product> products, List<Accounts.User> customers, DateTimeOffset now)
    {
        var (orderStatus, paymentStatus, method) = Cycle[index % OrdersPerCycle];
        var isCounter = method == PaymentMethod.Cash;
        var customer = customers[index % customers.Count];
        var createdAt = now.AddDays(-HistoryDays + (index * HistoryDays / (OrderCount - 1))).AddHours(-(index % 5));
        var items = CreateItems(index, products);
        var subtotal = items.Sum(item => item.UnitPriceMinor * item.Quantity);
        var hasCoupon = !isCounter && index % CouponEvery == 0;
        var discount = hasCoupon ? subtotal * WelcomeDiscountBasisPoints / BasisPointsBase : 0;
        var deliveryFee = !isCounter && index % DeliveryEvery == 0 ? DeliveryFeeMinor : 0;
        var total = subtotal - discount + deliveryFee;

        return new Order
        {
            Number = $"UN-{index + 1:D6}",
            UserId = isCounter ? null : customer.Id,
            CustomerName = isCounter ? CounterCustomerName : customer.Name,
            CustomerPhone = isCounter ? string.Empty : customer.Phone!,
            Channel = isCounter ? OrderChannel.Counter : OrderChannel.Online,
            Fulfillment = deliveryFee > 0 ? FulfillmentKind.Delivery : FulfillmentKind.Pickup,
            DeliveryAddress = deliveryFee > 0 ? "Avenida 24 de Julho, Maputo" : null,
            Status = orderStatus,
            SubtotalMinor = subtotal,
            DiscountMinor = discount,
            DeliveryFeeMinor = deliveryFee,
            TotalMinor = total,
            CouponCode = hasCoupon ? WelcomeCoupon : null,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddMinutes(OrderUpdateMinutes),
            Items = items,
            Payments = [CreatePayment(isCounter ? null : customer.Phone, total, method, paymentStatus, createdAt)],
        };
    }

    private static List<OrderItem> CreateItems(int index, List<Product> products)
    {
        var first = products[index * FirstProductStep % products.Count];
        var second = products[((index * SecondProductStep) + SecondProductOffset) % products.Count];
        return new[] { (first, 1 + (index % 2)), (second, 1) }
            .DistinctBy(line => line.Item1.Id)
            .Select(line => new OrderItem
            {
                ProductId = line.Item1.Id,
                LocationId = line.Item1.Kind == ProductKind.Digital ? null : line.Item1.Stocks.MinBy(stock => stock.LocationId)?.LocationId,
                Kind = line.Item1.Kind,
                ProductName = line.Item1.Name,
                ImageUrl = line.Item1.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault() ?? string.Empty,
                UnitPriceMinor = line.Item1.PriceMinor,
                Quantity = line.Item2,
            })
            .ToList();
    }

    private static Payment CreatePayment(
        string? phone, long total, PaymentMethod method, PaymentStatus status, DateTimeOffset createdAt)
    {
        var isCash = method == PaymentMethod.Cash;
        return new Payment
        {
            Provider = isCash ? CounterProvider : MockProvider,
            Method = method,
            Phone = phone,
            AmountMinor = total,
            FeeMinor = status == PaymentStatus.Paid && !isCash ? total * MobileFeePercent / PercentBase : 0,
            Status = status,
            ProviderReference = isCash ? null : $"MOCK-{createdAt.ToUnixTimeSeconds()}",
            FailureReason = status == PaymentStatus.Failed ? InsufficientBalanceReason : null,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddMinutes(1),
        };
    }
}
