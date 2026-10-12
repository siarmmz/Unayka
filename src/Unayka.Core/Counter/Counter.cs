using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Core.Counter;

/// <summary>Cash drawer period opened and closed by a cashier. Amounts are in minor units.</summary>
public sealed class CashShift
{
    public int Id { get; set; }
    public int OpenedByUserId { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public long OpeningMinor { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public long? CountedMinor { get; set; }
    public long? ExpectedMinor { get; set; }
}

/// <summary>Line of a counter ticket. ProductId is zero for a free amount line.</summary>
public sealed record TicketLine(int ProductId, string Name, long UnitPriceMinor, int Quantity)
{
    public const int FreeAmountProductId = 0;

    public long TotalMinor => UnitPriceMinor * Quantity;

    public bool IsFreeAmount => ProductId == FreeAmountProductId;
}

public static class Ticket
{
    public static long Total(IEnumerable<TicketLine> lines) => lines.Sum(line => line.TotalMinor);
}

/// <summary>Total of paid counter sales for one payment method.</summary>
public sealed record MethodTotal(PaymentMethod Method, int Count, long TotalMinor);

/// <summary>Sales figures of a cash shift derived from its paid payments.</summary>
public sealed record ShiftTotals(
    IReadOnlyList<MethodTotal> ByMethod,
    int SalesCount,
    long SalesMinor,
    long CashSalesMinor,
    long ExpectedCashMinor);

public static class CashMath
{
    private static readonly PaymentMethod[] AllMethods = Enum.GetValues<PaymentMethod>();

    public static long ExpectedCash(long openingMinor, long cashSalesMinor) => openingMinor + cashSalesMinor;

    public static long Difference(long countedMinor, long expectedMinor) => countedMinor - expectedMinor;

    public static bool IsSufficient(long receivedMinor, long totalMinor) => receivedMinor >= totalMinor;

    public static long ChangeDue(long receivedMinor, long totalMinor) => Math.Max(0, receivedMinor - totalMinor);

    public static ShiftTotals Summarize(long openingMinor, IEnumerable<(PaymentMethod Method, long AmountMinor)> payments)
    {
        var list = payments.ToList();
        var byMethod = AllMethods
            .Select(method => new MethodTotal(
                method,
                list.Count(payment => payment.Method == method),
                list.Where(payment => payment.Method == method).Sum(payment => payment.AmountMinor)))
            .ToList();
        var cashSales = byMethod.Single(total => total.Method == PaymentMethod.Cash).TotalMinor;
        return new ShiftTotals(byMethod, list.Count, byMethod.Sum(total => total.TotalMinor), cashSales, ExpectedCash(openingMinor, cashSales));
    }
}

public static class MoneyInput
{
    private const int MinorUnitsPerUnit = 100;
    private const decimal MaximumAmount = 10_000_000m;

    public static bool TryParse(string? text, out long minorUnits)
    {
        minorUnits = 0;
        var normalized = (text ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount < 0 || amount > MaximumAmount)
        {
            return false;
        }

        minorUnits = (long)Math.Round(amount * MinorUnitsPerUnit, MidpointRounding.AwayFromZero);
        return true;
    }

    public static long ParseOrZero(string? text) => TryParse(text, out var minor) ? minor : 0;
}

public static class PreparationFlow
{
    public static OrderStatus? Next(OrderStatus status) => status switch
    {
        OrderStatus.New or OrderStatus.Paid => OrderStatus.Preparing,
        OrderStatus.Preparing => OrderStatus.Ready,
        OrderStatus.Ready => OrderStatus.Delivered,
        _ => null,
    };

    public static OrderStatus InitialStatus(bool needsPreparation) => needsPreparation ? OrderStatus.Preparing : OrderStatus.Delivered;
}

public static class CounterTimes
{
    private static readonly TimeSpan MaputoOffset = TimeSpan.FromHours(2);

    public static string FormatTime(DateTimeOffset value) =>
        value.ToOffset(MaputoOffset).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTimeOffset value) =>
        value.ToOffset(MaputoOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static DateTimeOffset StartOfDay(DateTimeOffset now)
    {
        var local = now.ToOffset(MaputoOffset);
        return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, MaputoOffset);
    }

    public static int AgeInMinutes(DateTimeOffset createdAt, DateTimeOffset now) =>
        (int)Math.Max(0, (now - createdAt).TotalMinutes);
}

public static class CounterProviders
{
    public const string Cash = "counter-cash";
    public const string Card = "counter-terminal";
    public const string Wallet = "counter-wallet";
}

/// <summary>Card of the preparation board.</summary>
public sealed record PreparationCard(
    int OrderId,
    string Number,
    string CustomerName,
    OrderChannel Channel,
    OrderStatus Status,
    IReadOnlyList<string> Items,
    int AgeMinutes);

/// <summary>Orders grouped by preparation column.</summary>
public sealed record PreparationBoard(
    IReadOnlyList<PreparationCard> Incoming,
    IReadOnlyList<PreparationCard> Preparing,
    IReadOnlyList<PreparationCard> Ready,
    IReadOnlyList<PreparationCard> DeliveredToday);

public interface ICounterPreparation
{
    /// <summary>Loads the current board with online and counter orders.</summary>
    Task<PreparationBoard> LoadBoardAsync(CancellationToken cancellationToken = default);

    /// <summary>Moves the order to its next status and writes an audit entry. Returns false when it cannot advance.</summary>
    Task<bool> AdvanceAsync(int orderId, int userId, CancellationToken cancellationToken = default);
}

/// <summary>Product shown as a tile in the point of sale grid.</summary>
public sealed record CounterProduct(int Id, string Name, int CategoryId, long PriceMinor, string ImageUrl, int StockQuantity);

/// <summary>Category tab of the point of sale grid.</summary>
public sealed record CounterCategory(int Id, string Name);

/// <summary>Products and categories available for sale at the counter.</summary>
public sealed record CounterCatalog(IReadOnlyList<CounterCategory> Categories, IReadOnlyList<CounterProduct> Products);

/// <summary>One sale ready to be registered. WalletPhone is required for M-Pesa, e-Mola and mKesh.</summary>
public sealed record CounterSaleRequest(
    int CashierId,
    IReadOnlyList<TicketLine> Lines,
    PaymentMethod Method,
    string CustomerName,
    string CustomerPhone,
    string? WalletPhone,
    long ReceivedMinor);

public enum CounterSaleError
{
    None,
    NoOpenShift,
    EmptyTicket,
    InvalidAmount,
    InvalidWalletPhone,
    InsufficientCash,
    OutOfStock,
}

/// <summary>Receipt of a registered sale.</summary>
public sealed record CounterReceipt(
    string Number,
    DateTimeOffset At,
    IReadOnlyList<TicketLine> Lines,
    long TotalMinor,
    PaymentMethod Method,
    long ReceivedMinor,
    long ChangeMinor,
    string CustomerName,
    OrderStatus Status);

/// <summary>Outcome of registering a sale: a receipt or an error.</summary>
public sealed record CounterSaleResult(CounterReceipt? Receipt, CounterSaleError Error)
{
    public bool Succeeded => Receipt is not null;
}

public interface ICounterSales
{
    /// <summary>Loads active categories and products in stock order for the grid.</summary>
    Task<CounterCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates the ticket and creates the paid order, payment and stock movement.</summary>
    Task<CounterSaleResult> RegisterAsync(CounterSaleRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Row of the closed shift history.</summary>
public sealed record ShiftHistoryRow(
    int Id,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    long OpeningMinor,
    long ExpectedMinor,
    long? CountedMinor);

/// <summary>Open shift with its sales figures.</summary>
public sealed record ShiftSummary(CashShift Shift, ShiftTotals Totals);

public interface ICounterShifts
{
    /// <summary>Returns the shift that is currently open, if any.</summary>
    Task<CashShift?> GetOpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a shift with the given opening amount, or returns the already open one.</summary>
    Task<CashShift> OpenAsync(int userId, long openingMinor, CancellationToken cancellationToken = default);

    /// <summary>Sums the paid counter payments registered since the shift was opened.</summary>
    Task<ShiftSummary> SummarizeAsync(CashShift shift, CancellationToken cancellationToken = default);

    /// <summary>Closes the open shift with the counted cash and returns the final summary.</summary>
    Task<ShiftSummary?> CloseAsync(int userId, long countedCashMinor, string? note, CancellationToken cancellationToken = default);

    /// <summary>Lists the most recent shifts, newest first.</summary>
    Task<IReadOnlyList<ShiftHistoryRow>> GetHistoryAsync(int count, CancellationToken cancellationToken = default);
}

public static class CounterServiceExtensions
{
    public static IServiceCollection AddUnaykaCounter(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICounterShifts, CounterShifts>();
        services.AddScoped<ICounterSales, CounterSales>();
        services.AddScoped<ICounterPreparation, CounterPreparation>();
        return services;
    }
}

public sealed class CounterPreparation(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit, TimeProvider time) : ICounterPreparation
{
    private const string OrderEntity = "Order";

    private static readonly OrderStatus[] BoardStatuses =
        [OrderStatus.New, OrderStatus.Paid, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Delivered];

    public async Task<PreparationBoard> LoadBoardAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var dayStart = CounterTimes.StartOfDay(now);
        var orders = await database.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => BoardStatuses.Contains(order.Status) && (order.Status != OrderStatus.Delivered || order.UpdatedAt >= dayStart))
            .OrderBy(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
        var cards = orders.Select(order => ToCard(order, now)).ToList();
        return new PreparationBoard(
            cards.Where(card => card.Status is OrderStatus.New or OrderStatus.Paid).ToList(),
            cards.Where(card => card.Status == OrderStatus.Preparing).ToList(),
            cards.Where(card => card.Status == OrderStatus.Ready).ToList(),
            cards.Where(card => card.Status == OrderStatus.Delivered).OrderByDescending(card => card.OrderId).ToList());
    }

    public async Task<bool> AdvanceAsync(int orderId, int userId, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var order = await database.Orders.FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
        var next = order is null ? null : PreparationFlow.Next(order.Status);
        if (order is null || next is null)
        {
            return false;
        }

        var previous = order.Status;
        order.Status = next.Value;
        order.UpdatedAt = time.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(userId, "order.status", OrderEntity, order.Id.ToString(CultureInfo.InvariantCulture), $"{order.Number} {StatusLabels.For(previous).Label} to {StatusLabels.For(next.Value).Label}", cancellationToken);
        return true;
    }

    private static PreparationCard ToCard(Order order, DateTimeOffset now) => new(
        order.Id,
        order.Number,
        order.CustomerName,
        order.Channel,
        order.Status,
        order.Items.Select(item => $"{item.Quantity} x {item.ProductName}").ToList(),
        CounterTimes.AgeInMinutes(order.CreatedAt, now));
}

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
        var needsPreparation = lines.Any(line => !line.IsFreeAmount && products[line.ProductId].Category?.Section?.RequiresPreparation == true);
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
            PaymentMethod.Cash => CounterProviders.Cash,
            PaymentMethod.Card => CounterProviders.Card,
            _ => CounterProviders.Wallet,
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

public sealed class CounterShifts(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit, TimeProvider time) : ICounterShifts
{
    private const string ShiftEntity = "CashShift";

    public async Task<CashShift?> GetOpenAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        return await database.CashShifts.AsNoTracking().FirstOrDefaultAsync(shift => shift.ClosedAt == null, cancellationToken);
    }

    public async Task<CashShift> OpenAsync(int userId, long openingMinor, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.CashShifts.FirstOrDefaultAsync(shift => shift.ClosedAt == null, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var shift = new CashShift { OpenedByUserId = userId, OpenedAt = time.GetUtcNow(), OpeningMinor = Math.Max(0, openingMinor) };
        database.CashShifts.Add(shift);
        await database.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(userId, "shift.open", ShiftEntity, shift.Id.ToString(CultureInfo.InvariantCulture), $"Opening {Formatters.FormatMoney(shift.OpeningMinor)}", cancellationToken);
        return shift;
    }

    public async Task<ShiftSummary> SummarizeAsync(CashShift shift, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var end = shift.ClosedAt ?? time.GetUtcNow();
        var payments = await database.Payments.AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Paid
                              && payment.Order!.Channel == OrderChannel.Counter
                              && payment.Order.CreatedAt >= shift.OpenedAt
                              && payment.Order.CreatedAt <= end)
            .Select(payment => new { payment.Method, payment.AmountMinor })
            .ToListAsync(cancellationToken);
        var totals = CashMath.Summarize(shift.OpeningMinor, payments.Select(payment => (payment.Method, payment.AmountMinor)));
        return new ShiftSummary(shift, totals);
    }

    public async Task<ShiftSummary?> CloseAsync(int userId, long countedCashMinor, string? note, CancellationToken cancellationToken = default)
    {
        var open = await GetOpenAsync(cancellationToken);
        if (open is null)
        {
            return null;
        }

        var summary = await SummarizeAsync(open, cancellationToken);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var shift = await database.CashShifts.SingleAsync(candidate => candidate.Id == open.Id, cancellationToken);
        shift.ClosedAt = time.GetUtcNow();
        shift.CountedMinor = countedCashMinor;
        shift.ExpectedMinor = summary.Totals.ExpectedCashMinor;
        await database.SaveChangesAsync(cancellationToken);

        var difference = CashMath.Difference(countedCashMinor, summary.Totals.ExpectedCashMinor);
        var detail = string.IsNullOrWhiteSpace(note) ? string.Empty : $" {note.Trim()}";
        await audit.WriteAsync(userId, "shift.close", ShiftEntity, shift.Id.ToString(CultureInfo.InvariantCulture), $"Cash difference {Formatters.FormatMoney(difference)}.{detail}", cancellationToken);
        return new ShiftSummary(shift, summary.Totals);
    }

    public async Task<IReadOnlyList<ShiftHistoryRow>> GetHistoryAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var shifts = await database.CashShifts.AsNoTracking()
            .Where(shift => shift.ClosedAt != null)
            .OrderByDescending(shift => shift.OpenedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
        return shifts
            .Select(shift => new ShiftHistoryRow(shift.Id, shift.OpenedAt, shift.ClosedAt, shift.OpeningMinor, shift.ExpectedMinor ?? 0, shift.CountedMinor))
            .ToList();
    }
}
