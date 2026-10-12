using Unayka.Core.Catalog;

namespace Unayka.Core.Locations;

/// <summary>Quantity of a physical product available at one location.</summary>
public sealed class LocationStock
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int LocationId { get; set; }
    public Location? Location { get; set; }
    public int Quantity { get; set; }
}
