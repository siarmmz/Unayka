using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Carts;

public sealed class CartService(UnaykaDbContext database, TimeProvider time) : ICartService
{
    public const int MaxQuantityPerLine = 20;

    public async Task<CartView> GetAsync(Guid? cartId, CancellationToken cancellationToken = default)
    {
        if (cartId is null)
        {
            return CartView.Empty;
        }

        var cart = await database.Carts.AsNoTracking()
            .Where(candidate => candidate.Id == cartId)
            .Select(candidate => new { candidate.CouponCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (cart is null)
        {
            return CartView.Empty;
        }

        var lines = await LoadLinesAsync(cartId.Value, cancellationToken);
        var coupon = cart.CouponCode is null
            ? null
            : await database.Coupons.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Code == cart.CouponCode, cancellationToken);
        return CartCalculator.Build(lines, coupon, time.GetUtcNow());
    }

    public async Task<CartChangeResult> AddAsync(
        Guid? cartId, int? userId, int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var product = await database.Products.AsNoTracking()
            .Where(candidate => candidate.Id == productId && candidate.IsActive)
            .Select(candidate => new { candidate.StockQuantity })
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return new CartChangeResult(cartId ?? Guid.Empty, CartChange.UnknownProduct);
        }

        var cart = await FindOrCreateAsync(cartId, userId, cancellationToken);
        if (product.StockQuantity <= 0)
        {
            await database.SaveChangesAsync(cancellationToken);
            return new CartChangeResult(cart.Id, CartChange.OutOfStock);
        }

        var line = await database.CartItems.FirstOrDefaultAsync(
            item => item.CartId == cart.Id && item.ProductId == productId, cancellationToken);
        var wanted = (line?.Quantity ?? 0) + Math.Max(quantity, 1);
        var allowed = Math.Min(wanted, Math.Min(product.StockQuantity, MaxQuantityPerLine));
        if (line is null)
        {
            database.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = productId, Quantity = allowed });
        }
        else
        {
            line.Quantity = allowed;
        }

        cart.UpdatedAt = time.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        return new CartChangeResult(cart.Id, allowed < wanted ? CartChange.LimitedByStock : CartChange.Added);
    }

    public async Task SetQuantityAsync(Guid cartId, int productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            await RemoveAsync(cartId, productId, cancellationToken);
            return;
        }

        var line = await database.CartItems.Include(item => item.Product)
            .FirstOrDefaultAsync(item => item.CartId == cartId && item.ProductId == productId, cancellationToken);
        if (line?.Product is null)
        {
            return;
        }

        line.Quantity = Math.Max(1, Math.Min(quantity, Math.Min(line.Product.StockQuantity, MaxQuantityPerLine)));
        await TouchAsync(cartId, cancellationToken);
    }

    public async Task RemoveAsync(Guid cartId, int productId, CancellationToken cancellationToken = default)
    {
        await database.CartItems
            .Where(item => item.CartId == cartId && item.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);
        await TouchAsync(cartId, cancellationToken);
    }

    public async Task ClearAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        await database.CartItems.Where(item => item.CartId == cartId).ExecuteDeleteAsync(cancellationToken);
        var cart = await database.Carts.FindAsync([cartId], cancellationToken);
        if (cart is null)
        {
            return;
        }

        cart.CouponCode = null;
        cart.UpdatedAt = time.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<CouponApplyResult> ApplyCouponAsync(Guid cartId, string? code, CancellationToken cancellationToken = default)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        var coupon = normalized.Length == 0
            ? null
            : await database.Coupons.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Code == normalized, cancellationToken);
        if (coupon is null)
        {
            return new CouponApplyResult(CouponApplyOutcome.Unknown, null);
        }

        var lines = await LoadLinesAsync(cartId, cancellationToken);
        var view = CartCalculator.Build(lines, coupon, time.GetUtcNow());
        if (view.CouponRejection is not null)
        {
            return new CouponApplyResult(CouponApplyOutcome.Rejected, view.CouponRejection);
        }

        await SetCouponAsync(cartId, coupon.Code, cancellationToken);
        return new CouponApplyResult(CouponApplyOutcome.Applied, null);
    }

    public Task RemoveCouponAsync(Guid cartId, CancellationToken cancellationToken = default) =>
        SetCouponAsync(cartId, null, cancellationToken);

    private async Task SetCouponAsync(Guid cartId, string? code, CancellationToken cancellationToken)
    {
        var cart = await database.Carts.FindAsync([cartId], cancellationToken);
        if (cart is null)
        {
            return;
        }

        cart.CouponCode = code;
        cart.UpdatedAt = time.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task TouchAsync(Guid cartId, CancellationToken cancellationToken)
    {
        var cart = await database.Carts.FindAsync([cartId], cancellationToken);
        if (cart is not null)
        {
            cart.UpdatedAt = time.GetUtcNow();
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<Cart> FindOrCreateAsync(Guid? cartId, int? userId, CancellationToken cancellationToken)
    {
        var cart = cartId is null ? null : await database.Carts.FindAsync([cartId.Value], cancellationToken);
        if (cart is null)
        {
            cart = new Cart { Id = Guid.NewGuid(), UserId = userId, UpdatedAt = time.GetUtcNow() };
            database.Carts.Add(cart);
        }

        cart.UserId ??= userId;
        return cart;
    }

    private async Task<List<CartLineView>> LoadLinesAsync(Guid cartId, CancellationToken cancellationToken)
    {
        var rows = await database.CartItems.AsNoTracking()
            .Where(item => item.CartId == cartId)
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                item.ProductId,
                item.Quantity,
                item.Product!.Slug,
                item.Product.Name,
                item.Product.CategoryId,
                item.Product.PriceMinor,
                item.Product.StockQuantity,
                item.Product.IsActive,
                Image = item.Product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new CartLineView(
                row.ProductId, row.Slug, row.Name, row.Image ?? string.Empty, row.CategoryId,
                row.PriceMinor, row.Quantity, row.StockQuantity, row.IsActive && row.StockQuantity > 0))
            .ToList();
    }
}
