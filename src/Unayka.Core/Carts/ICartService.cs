namespace Unayka.Core.Carts;

public interface ICartService
{
    /// <summary>Returns the cart with totals recalculated from current prices, or an empty cart.</summary>
    Task<CartView> GetAsync(Guid? cartId, CancellationToken cancellationToken = default);

    /// <summary>Adds a product, creating the cart when needed, never above the available stock.</summary>
    Task<CartChangeResult> AddAsync(Guid? cartId, int? userId, int productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Sets the quantity of a line, limited by stock. Zero or less removes the line.</summary>
    Task SetQuantityAsync(Guid cartId, int productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Removes one line from the cart.</summary>
    Task RemoveAsync(Guid cartId, int productId, CancellationToken cancellationToken = default);

    /// <summary>Removes every line and the coupon.</summary>
    Task ClearAsync(Guid cartId, CancellationToken cancellationToken = default);

    /// <summary>Validates the code against the cart and stores it on the cart when accepted.</summary>
    Task<CouponApplyResult> ApplyCouponAsync(Guid cartId, string? code, CancellationToken cancellationToken = default);

    /// <summary>Detaches the coupon from the cart.</summary>
    Task RemoveCouponAsync(Guid cartId, CancellationToken cancellationToken = default);
}
