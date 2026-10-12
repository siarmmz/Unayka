using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Core.Counter;

public sealed class CounterSales(IDbContextFactory<UnaykaDbContext> factory, IOrderNumberGenerator orderNumbers, IAuditLog audit, TimeProvider time) : ICounterSales
{
    private const string OrderEntity = "Order";
    private const string DefaultCustomerName = "Cliente balcão";

    public async Task<CounterCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var categories = await database.Categories.AsNoTracking()
            .Where(category => category.Status == CatalogStatus.Active && category.Section!.Status == CatalogStatus.Active)
            .OrderBy(category => category.Section!.SortOrder)
            .ThenBy(category => category.SortOrder)
            .Select(category => new CounterCategory(category.Id, category.Name))
            .ToListAsync(cancellationToken);
        var products = await database.Products.AsNoTracking()
            .Where(ProductVisibility.IsPublic)
            .Where(product => product.Kind == ProductKind.Physical)
            .OrderBy(product => product.Name)
            .Select(product => new CounterProduct(
                product.Id,
                product.Name,
                product.CategoryId,
                product.PriceMinor,
                product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault() ?? string.Empty,
                product.Stocks.Where(stock => stock.Location!.Status == CatalogStatus.Active).Sum(stock => stock.Quantity)))
            .ToListAsync(cancellationToken);
        return new CounterCatalog(categories, products);
    }

    public async Task<CounterSaleResult> RegisterAsync(CounterSaleRequest request, CancellationToken cancellationToken = default)
    {
        var invalid = Validate(request);
        if (invalid != CounterSaleError.None)
        {
            return Failure(invalid);
        }

        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        if (!await database.CashShifts.AnyAsync(shift => shift.ClosedAt == null, cancellationToken))
        {
            return Failure(CounterSaleError.NoOpenShift);
        }

        var productIds = request.Lines.Where(line => !line.IsFreeAmount).Select(line => line.ProductId).Distinct().ToList();
        var products = await database.Products
            .Include(product => product.Category).ThenInclude(category => category!.Section)
            .Include(product => product.Stocks).ThenInclude(stock => stock.Location)
            .Include(product => product.Media)
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        if (productIds.Any(id => !products.ContainsKey(id)))
        {
            return Failure(CounterSaleError.OutOfStock);
        }

        var lines = request.Lines.Select(line => line.IsFreeAmount ? line : line with { Name = products[line.ProductId].Name, UnitPriceMinor = products[line.ProductId].PriceMinor }).ToList();
        if (HasUnavailableProduct(lines, products))
        {
            return Failure(CounterSaleError.OutOfStock);
        }

        var total = Ticket.Total(lines);
        if (request.Method == PaymentMethod.Cash && !CashMath.IsSufficient(request.ReceivedMinor, total))
        {
            return Failure(CounterSaleError.InsufficientCash);
        }

        var now = time.GetUtcNow();
        var needsPreparation = lines.Any(line => !line.IsFreeAmount && products[line.ProductId].Category?.Section?.Slug == SectionSlugs.Teas);
        var items = lines.SelectMany(line => line.IsFreeAmount ? [ToFreeAmountItem(line)] : Reserve(line, products[line.ProductId])).ToList();
        var order = BuildOrder(request, items, total, PreparationFlow.InitialStatus(needsPreparation), await orderNumbers.NextAsync(cancellationToken), now);
        database.Orders.Add(order);

        await database.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(request.CashierId, "order.counter", OrderEntity, order.Id.ToString(CultureInfo.InvariantCulture), $"{order.Number} {Formatters.FormatMoney(total)} {StatusLabels.For(request.Method)}", cancellationToken);

        var received = request.Method == PaymentMethod.Cash ? request.ReceivedMinor : total;
        var receipt = new CounterReceipt(order.Number, now, lines, total, request.Method, received, CashMath.ChangeDue(received, total), order.CustomerName, order.Status);
        return new CounterSaleResult(receipt, CounterSaleError.None);
    }

    private static CounterSaleError Validate(CounterSaleRequest request)
    {
        if (request.Lines.Count == 0)
        {
            return CounterSaleError.EmptyTicket;
        }

        if (request.Lines.Any(line => line.Quantity <= 0 || line.UnitPriceMinor <= 0 || (line.IsFreeAmount && string.IsNullOrWhiteSpace(line.Name))))
        {
            return CounterSaleError.InvalidAmount;
        }

        return request.Method.IsWallet() && PhoneNumber.Normalize(request.WalletPhone) is null
            ? CounterSaleError.InvalidWalletPhone
            : CounterSaleError.None;
    }

    private static bool HasUnavailableProduct(IEnumerable<TicketLine> lines, Dictionary<int, Product> products) =>
        lines.Where(line => !line.IsFreeAmount).Any(line => !products.TryGetValue(line.ProductId, out var product) || !IsSellable(product) || ProductStock.Total(product) < line.Quantity);

    private static bool IsSellable(Product product) =>
        product.Kind == ProductKind.Physical
        && product.Status == CatalogStatus.Active
        && product.Category?.Status == CatalogStatus.Active
        && product.Category.Section?.Status == CatalogStatus.Active;

    private static OrderItem ToFreeAmountItem(TicketLine line) => new()
    {
        ProductId = line.ProductId,
        Kind = ProductKind.Physical,
        ProductName = line.Name.Trim(),
        UnitPriceMinor = line.UnitPriceMinor,
        Quantity = line.Quantity,
    };

    private static IEnumerable<OrderItem> Reserve(TicketLine line, Product product)
    {
        product.SalesCount += line.Quantity;
        var image = product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault() ?? string.Empty;
        var stocks = product.Stocks.Where(stock => stock.Location?.Status == CatalogStatus.Active).OrderBy(stock => stock.Location!.SortOrder).ToList();
        var allocations = StockAllocator.Allocate(stocks, line.Quantity) ?? [];
        foreach (var allocation in allocations)
        {
            stocks.First(stock => stock.LocationId == allocation.LocationId).Quantity -= allocation.Quantity;
        }

        return allocations.Select(allocation => ToItem(line, product, image, allocation.LocationId, allocation.Quantity));
    }

    private static OrderItem ToItem(TicketLine line, Product product, string image, int? locationId, int quantity) => new()
    {
        ProductId = product.Id,
        LocationId = locationId,
        Kind = product.Kind,
        ProductName = line.Name.Trim(),
        ImageUrl = image,
        UnitPriceMinor = line.UnitPriceMinor,
        Quantity = quantity,
    };

    private static CounterSaleResult Failure(CounterSaleError error) => new(null, error);

    private static Order BuildOrder(CounterSaleRequest request, List<OrderItem> items, long total, OrderStatus status, string number, DateTimeOffset now) => new()
    {
        Number = number,
        CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? DefaultCustomerName : request.CustomerName.Trim(),
        CustomerPhone = PhoneNumber.Normalize(request.CustomerPhone) ?? string.Empty,
        Channel = OrderChannel.Counter,
        Fulfillment = FulfillmentKind.Pickup,
        Status = status,
        SubtotalMinor = total,
        TotalMinor = total,
        CreatedAt = now,
        UpdatedAt = now,
        Items = items,
        Payments = [BuildPayment(request, total, now)],
    };

    private static Payment BuildPayment(CounterSaleRequest request, long total, DateTimeOffset now) => new()
    {
        Provider = request.Method switch
        {
            PaymentMethod.Cash => CounterWallet.CashProvider,
            PaymentMethod.Card => CounterWallet.CardProvider,
            _ => CounterWallet.Provider,
        },
        Method = request.Method,
        Phone = request.Method.IsWallet() ? PhoneNumber.Normalize(request.WalletPhone) : null,
        AmountMinor = total,
        Status = PaymentStatus.Paid,
        ProviderReference = Guid.NewGuid().ToString("N"),
        CreatedAt = now,
        UpdatedAt = now,
    };
}
