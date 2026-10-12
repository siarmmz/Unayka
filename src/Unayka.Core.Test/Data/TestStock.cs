using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Test.Data;

public static class TestStock
{
    public static async Task SetTotalAsync(UnaykaDbContext database, int productId, int quantity)
    {
        var stocks = await database.LocationStocks.Where(stock => stock.ProductId == productId).OrderBy(stock => stock.LocationId).ToListAsync();
        foreach (var stock in stocks)
        {
            stock.Quantity = 0;
        }

        stocks[0].Quantity = quantity;
        await database.SaveChangesAsync();
    }

    public static async Task<int> TotalAsync(UnaykaDbContext database, int productId) =>
        await database.LocationStocks.AsNoTracking().Where(stock => stock.ProductId == productId).SumAsync(stock => stock.Quantity);
}
