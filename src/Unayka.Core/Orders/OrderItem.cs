using Unayka.Core.Catalog;

namespace Unayka.Core.Orders;

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
