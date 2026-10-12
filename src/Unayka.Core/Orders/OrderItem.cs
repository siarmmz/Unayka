namespace Unayka.Core.Orders;

/// <summary>Line of an order with the name and price frozen at purchase time.</summary>
public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long UnitPriceMinor { get; set; }
    public int Quantity { get; set; }
}
