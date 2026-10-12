using Unayka.Core.Catalog;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Counter;

/// <summary>Product shown as a tile in the point of sale grid.</summary>
public sealed record CounterProduct(int Id, string Name, int CategoryId, long PriceMinor, string ImageUrl, int StockQuantity);

/// <summary>Category tab of the point of sale grid.</summary>
public sealed record CounterCategory(int Id, string Name, CategoryKind Kind);

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
