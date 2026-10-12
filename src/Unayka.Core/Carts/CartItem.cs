using Unayka.Core.Catalog;

namespace Unayka.Core.Carts;

/// <summary>Line of a cart. Prices are always read from the product, never stored here.</summary>
public sealed class CartItem
{
    public int Id { get; set; }
    public Guid CartId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
}
