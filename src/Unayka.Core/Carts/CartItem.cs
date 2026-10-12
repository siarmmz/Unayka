using Unayka.Core.Catalog;

namespace Unayka.Core.Carts;

/// <summary>Line of a cart. Prices are always read from the product, never stored here. LocationId is the chosen pickup location of a physical line, null means the first location with stock.</summary>
public sealed class CartItem
{
    public int Id { get; set; }
    public Guid CartId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public int? LocationId { get; set; }
}
