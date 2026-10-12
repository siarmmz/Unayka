using Microsoft.EntityFrameworkCore;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Orders;

namespace Unayka.Core.Administration;

/// <summary>Best selling product in the last 30 days.</summary>
public sealed record TopProduct(int ProductId, string Name, int Quantity, long RevenueMinor);

/// <summary>Order line shown in compact lists.</summary>
public sealed record OrderSummary(int Id, string Number, string CustomerName, OrderChannel Channel, OrderStatus Status, long TotalMinor, DateTimeOffset CreatedAt);

/// <summary>Active product with little stock left.</summary>
public sealed record LowStockProduct(int Id, string Name, int StockQuantity, string? ImageUrl);

/// <summary>Everything the dashboard page shows.</summary>
public sealed record DashboardData(
    DashboardFigures Figures,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<OrderSummary> LatestOrders,
    IReadOnlyList<LowStockProduct> LowStock);

public interface IAdminDashboard
{
    /// <summary>Loads figures, rankings and alerts for the dashboard.</summary>
    Task<DashboardData> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminDashboardService(IDbContextFactory<UnaykaDbContext> factory, TimeProvider time) : IAdminDashboard
{
    public const int LowStockThreshold = 5;
    private const int TopProductCount = 5;
    private const int LatestOrderCount = 8;
    private const int LowStockCount = 8;

    public async Task<DashboardData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var since = LocalDays.StartOfDay(LocalDays.ToLocalDay(now).AddDays(1 - DashboardCalculator.MonthDays));
        var open = AdminOrderGroups.Open;

        var samples = await db.Orders.AsNoTracking()
            .Where(order => order.CreatedAt >= since || open.Contains(order.Status))
            .Select(order => new OrderSample(order.CreatedAt, order.Status, order.TotalMinor))
            .ToListAsync(cancellationToken);

        return new DashboardData(
            DashboardCalculator.Compute(samples, now),
            await LoadTopProductsAsync(db, since, cancellationToken),
            await LoadLatestOrdersAsync(db, cancellationToken),
            await LoadLowStockAsync(db, cancellationToken));
    }

    private static async Task<IReadOnlyList<TopProduct>> LoadTopProductsAsync(UnaykaDbContext db, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var revenueStatuses = AdminOrderGroups.Revenue;
        return await (
                from item in db.OrderItems
                join order in db.Orders on item.OrderId equals order.Id
                where revenueStatuses.Contains(order.Status) && order.CreatedAt >= since
                group item by new { item.ProductId, item.ProductName }
                into grouped
                orderby grouped.Sum(item => item.Quantity) descending
                select new TopProduct(
                    grouped.Key.ProductId,
                    grouped.Key.ProductName,
                    grouped.Sum(item => item.Quantity),
                    grouped.Sum(item => item.UnitPriceMinor * item.Quantity)))
            .Take(TopProductCount)
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<OrderSummary>> LoadLatestOrdersAsync(UnaykaDbContext db, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .OrderByDescending(order => order.CreatedAt)
            .Take(LatestOrderCount)
            .Select(order => new OrderSummary(order.Id, order.Number, order.CustomerName, order.Channel, order.Status, order.TotalMinor, order.CreatedAt))
            .ToListAsync(cancellationToken);

    private static async Task<IReadOnlyList<LowStockProduct>> LoadLowStockAsync(UnaykaDbContext db, CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking()
            .Where(product => product.Status == CatalogStatus.Active
                              && product.Kind == ProductKind.Physical
                              && product.Stocks.Sum(stock => stock.Quantity) <= LowStockThreshold)
            .OrderBy(product => product.Stocks.Sum(stock => stock.Quantity))
            .ThenBy(product => product.Name)
            .Take(LowStockCount)
            .Select(product => new LowStockProduct(
                product.Id,
                product.Name,
                product.Stocks.Sum(stock => stock.Quantity),
                product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault()))
            .ToListAsync(cancellationToken);
}
