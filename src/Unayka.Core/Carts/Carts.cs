using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unayka.Core.Catalog;
using Unayka.Core.Coupons;
using Unayka.Core.Data;

namespace Unayka.Core.Carts;

/// <summary>Server side cart identified by the unayka_cart cookie.</summary>
public sealed class Cart
{
    public Guid Id { get; set; }
    public int? UserId { get; set; }
    public string? CouponCode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<CartItem> Items { get; set; } = [];
}

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

/// <summary>Pickup location that holds a product, with the quantity available there.</summary>
public sealed record CartLocationOption(int LocationId, string Name, int Quantity);

/// <summary>Cart line with the current product data. The unit price always comes from the product. Physical lines carry the pickup location they will be collected at.</summary>
public sealed record CartLineView(
    int ProductId,
    string Slug,
    string Name,
    string ImageUrl,
    int CategoryId,
    long UnitPriceMinor,
    int Quantity,
    int StockQuantity,
    bool IsAvailable,
    bool IsDigital,
    int? LocationId,
    string? LocationName,
    IReadOnlyList<CartLocationOption> LocationOptions)
{
    public long LineTotalMinor => UnitPriceMinor * Quantity;

    public bool ExceedsStock => !IsAvailable || Quantity > StockQuantity;
}

/// <summary>Physical cart lines collected at the same location.</summary>
public sealed record CartPickupGroup(int? LocationId, string LocationName, IReadOnlyList<CartLineView> Lines);

/// <summary>Cart totals recalculated on the server from current prices and the applied coupon.</summary>
public sealed record CartView(
    IReadOnlyList<CartLineView> Lines,
    long SubtotalMinor,
    long DiscountMinor,
    string? CouponCode,
    CouponRejection? CouponRejection)
{
    public static readonly CartView Empty = new([], 0, 0, null, null);

    public long TotalMinor => SubtotalMinor - DiscountMinor;

    public int ItemCount => Lines.Sum(line => line.Quantity);

    public bool IsEmpty => Lines.Count == 0;

    public bool HasStockProblem => Lines.Any(line => line.ExceedsStock);

    public bool HasDigital => Lines.Any(line => line.IsDigital);

    public bool IsDigitalOnly => HasDigital && Lines.All(line => line.IsDigital);

    public IReadOnlyList<CartLineView> DigitalLines => Lines.Where(line => line.IsDigital).ToList();

    public IReadOnlyList<CartPickupGroup> PickupGroups =>
        Lines.Where(line => !line.IsDigital)
            .GroupBy(line => line.LocationId)
            .Select(group => new CartPickupGroup(group.Key, group.First().LocationName ?? string.Empty, group.ToList()))
            .OrderBy(group => group.LocationName, StringComparer.Ordinal)
            .ToList();
}

public enum CartChange
{
    Added,
    LimitedByStock,
    OutOfStock,
    UnknownProduct,
    SignInRequired,
    LocationChanged,
}

public sealed record CartChangeResult(Guid CartId, CartChange Change);

public enum CouponApplyOutcome
{
    Applied,
    Unknown,
    Rejected,
}

public sealed record CouponApplyResult(CouponApplyOutcome Outcome, CouponRejection? Rejection);

public interface ICartService
{
    /// <summary>Returns the cart with totals recalculated from current prices, or an empty cart.</summary>
    Task<CartView> GetAsync(Guid? cartId, CancellationToken cancellationToken = default);

    /// <summary>Adds a product, creating the cart when needed, never above the available stock.</summary>
    Task<CartChangeResult> AddAsync(Guid? cartId, int? userId, int productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Sets the quantity of a line, limited by stock. Zero or less removes the line.</summary>
    Task SetQuantityAsync(Guid cartId, int productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Moves a physical line to another pickup location that has stock, limiting the quantity to the stock there.</summary>
    Task<CartChange> SetLocationAsync(Guid cartId, int productId, int locationId, CancellationToken cancellationToken = default);

    /// <summary>Removes one line from the cart.</summary>
    Task RemoveAsync(Guid cartId, int productId, CancellationToken cancellationToken = default);

    /// <summary>Removes every line and the coupon.</summary>
    Task ClearAsync(Guid cartId, CancellationToken cancellationToken = default);

    /// <summary>Validates the code against the cart and stores it on the cart when accepted.</summary>
    Task<CouponApplyResult> ApplyCouponAsync(Guid cartId, string? code, CancellationToken cancellationToken = default);

    /// <summary>Detaches the coupon from the cart.</summary>
    Task RemoveCouponAsync(Guid cartId, CancellationToken cancellationToken = default);
}

public static class CartCookie
{
    public const string Name = "unayka_cart";
}

public interface ICartSummary
{
    /// <summary>Total quantity of items in the cart, or zero when the cart does not exist.</summary>
    Task<int> CountItemsAsync(Guid? cartId, CancellationToken cancellationToken = default);
}

public sealed class CartSummary(UnaykaDbContext database) : ICartSummary
{
    public async Task<int> CountItemsAsync(Guid? cartId, CancellationToken cancellationToken = default)
    {
        if (cartId is null)
        {
            return 0;
        }

        return await database.CartItems
            .Where(item => item.CartId == cartId)
            .SumAsync(item => item.Quantity, cancellationToken);
    }
}

public static class CartCalculator
{
    public static CartView Build(IReadOnlyList<CartLineView> lines, Coupon? coupon, DateTimeOffset now)
    {
        var subtotal = lines.Sum(line => line.LineTotalMinor);
        if (coupon is null)
        {
            return new CartView(lines, subtotal, 0, null, null);
        }

        var evaluation = CouponEvaluator.Evaluate(
            coupon, lines.Select(line => new CouponLine(line.CategoryId, line.LineTotalMinor)).ToList(), now);
        return new CartView(lines, subtotal, evaluation.DiscountMinor, coupon.Code, evaluation.Rejection);
    }
}

public static class PickupLocations
{
    /// <summary>The chosen location when it is still offered, otherwise the first location with stock, otherwise null.</summary>
    public static CartLocationOption? Resolve(IReadOnlyList<CartLocationOption> optionsInOrder, int? chosenLocationId) =>
        optionsInOrder.FirstOrDefault(option => option.LocationId == chosenLocationId)
        ?? optionsInOrder.FirstOrDefault(option => option.Quantity > 0);
}

public static class CartServiceExtensions
{
    public static IServiceCollection AddUnaykaCarts(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICartSummary, CartSummary>();
        services.AddScoped<ICartService, CartService>();
        return services;
    }
}

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
            .Include(candidate => candidate.Stocks).ThenInclude(stock => stock.Location)
            .Where(ProductVisibility.IsPublic)
            .FirstOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);
        if (product is null)
        {
            return new CartChangeResult(cartId ?? Guid.Empty, CartChange.UnknownProduct);
        }

        if (product.Kind == ProductKind.Digital && userId is null)
        {
            return new CartChangeResult(cartId ?? Guid.Empty, CartChange.SignInRequired);
        }

        var cart = await FindOrCreateAsync(cartId, userId, cancellationToken);
        var line = await database.CartItems.FirstOrDefaultAsync(
            item => item.CartId == cart.Id && item.ProductId == productId, cancellationToken);
        var location = ResolveLocation(product, line?.LocationId);
        var available = AvailableAt(product, location);
        if (available <= 0)
        {
            await database.SaveChangesAsync(cancellationToken);
            return new CartChangeResult(cart.Id, CartChange.OutOfStock);
        }

        var wanted = (line?.Quantity ?? 0) + Math.Max(quantity, 1);
        var allowed = Math.Min(wanted, Math.Min(available, MaxQuantityPerLine));
        if (line is null)
        {
            database.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = productId, Quantity = allowed, LocationId = location?.LocationId });
        }
        else
        {
            line.Quantity = allowed;
            line.LocationId = location?.LocationId;
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

        var line = await database.CartItems.Include(item => item.Product).ThenInclude(product => product!.Stocks).ThenInclude(stock => stock.Location)
            .FirstOrDefaultAsync(item => item.CartId == cartId && item.ProductId == productId, cancellationToken);
        if (line?.Product is null)
        {
            return;
        }

        var location = ResolveLocation(line.Product, line.LocationId);
        line.Quantity = Math.Max(1, Math.Min(quantity, Math.Min(AvailableAt(line.Product, location), MaxQuantityPerLine)));
        await TouchAsync(cartId, cancellationToken);
    }

    public async Task<CartChange> SetLocationAsync(Guid cartId, int productId, int locationId, CancellationToken cancellationToken = default)
    {
        var line = await database.CartItems.Include(item => item.Product).ThenInclude(product => product!.Stocks).ThenInclude(stock => stock.Location)
            .FirstOrDefaultAsync(item => item.CartId == cartId && item.ProductId == productId, cancellationToken);
        if (line?.Product is null || line.Product.Kind == ProductKind.Digital)
        {
            return CartChange.UnknownProduct;
        }

        var target = LocationOptions(line.Product).FirstOrDefault(option => option.LocationId == locationId);
        if (target is null || target.Quantity <= 0)
        {
            return CartChange.OutOfStock;
        }

        var limited = line.Quantity > target.Quantity;
        line.LocationId = target.LocationId;
        line.Quantity = Math.Min(line.Quantity, target.Quantity);
        await TouchAsync(cartId, cancellationToken);
        return limited ? CartChange.LimitedByStock : CartChange.LocationChanged;
    }

    private static List<CartLocationOption> LocationOptions(Product product) =>
        product.Stocks
            .Where(stock => stock.Location?.Status == CatalogStatus.Active)
            .OrderBy(stock => stock.Location!.SortOrder)
            .ThenBy(stock => stock.Location!.Name)
            .Select(stock => new CartLocationOption(stock.LocationId, stock.Location!.Name, stock.Quantity))
            .ToList();

    private static CartLocationOption? ResolveLocation(Product product, int? chosenLocationId) =>
        product.Kind == ProductKind.Digital ? null : PickupLocations.Resolve(LocationOptions(product), chosenLocationId);

    private static int AvailableAt(Product product, CartLocationOption? location) =>
        product.Kind == ProductKind.Digital ? ProductStock.Unlimited : location?.Quantity ?? 0;

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
                item.Product.Kind,
                IsPublic = item.Product.Status == CatalogStatus.Active
                           && item.Product.Category!.Status == CatalogStatus.Active
                           && item.Product.Category.Section!.Status == CatalogStatus.Active,
                item.LocationId,
                Options = item.Product.Stocks
                    .Where(stock => stock.Location!.Status == CatalogStatus.Active)
                    .OrderBy(stock => stock.Location!.SortOrder)
                    .ThenBy(stock => stock.Location!.Name)
                    .Select(stock => new CartLocationOption(stock.LocationId, stock.Location!.Name, stock.Quantity))
                    .ToList(),
                Image = item.Product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row =>
            {
                var isDigital = row.Kind == ProductKind.Digital;
                var location = isDigital ? null : PickupLocations.Resolve(row.Options, row.LocationId);
                var stock = isDigital ? ProductStock.Unlimited : location?.Quantity ?? 0;
                return new CartLineView(
                    row.ProductId, row.Slug, row.Name, row.Image ?? string.Empty, row.CategoryId,
                    row.PriceMinor, row.Quantity, stock, row.IsPublic && stock > 0,
                    isDigital, location?.LocationId, location?.Name, isDigital ? [] : row.Options);
            })
            .ToList();
    }
}
