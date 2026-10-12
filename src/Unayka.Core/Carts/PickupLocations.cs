namespace Unayka.Core.Carts;

public static class PickupLocations
{
    /// <summary>The chosen location when it is still offered, otherwise the first location with stock, otherwise null.</summary>
    public static CartLocationOption? Resolve(IReadOnlyList<CartLocationOption> optionsInOrder, int? chosenLocationId) =>
        optionsInOrder.FirstOrDefault(option => option.LocationId == chosenLocationId)
        ?? optionsInOrder.FirstOrDefault(option => option.Quantity > 0);
}
