namespace Unayka.Core.Carts;

public static class CartCookie
{
    public const string Name = "unayka_cart";
}

public interface ICartSummary
{
    /// <summary>Total quantity of items in the cart, or zero when the cart does not exist.</summary>
    Task<int> CountItemsAsync(Guid? cartId, CancellationToken cancellationToken = default);
}
