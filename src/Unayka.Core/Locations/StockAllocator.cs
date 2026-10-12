namespace Unayka.Core.Locations;

/// <summary>Quantity taken from one location for an order line.</summary>
public sealed record StockAllocation(int LocationId, int Quantity);

public static class StockAllocator
{
    /// <summary>Takes the quantity from the first location that holds all of it, otherwise from the locations in order. Null when the total is not enough.</summary>
    public static IReadOnlyList<StockAllocation>? Allocate(IReadOnlyList<LocationStock> stocksInLocationOrder, int quantity)
    {
        if (stocksInLocationOrder.FirstOrDefault(stock => stock.Quantity >= quantity) is { } single)
        {
            return [new StockAllocation(single.LocationId, quantity)];
        }

        var allocations = new List<StockAllocation>();
        var remaining = quantity;
        foreach (var stock in stocksInLocationOrder.Where(stock => stock.Quantity > 0))
        {
            var taken = Math.Min(stock.Quantity, remaining);
            allocations.Add(new StockAllocation(stock.LocationId, taken));
            remaining -= taken;
        }

        return remaining == 0 ? allocations : null;
    }
}
