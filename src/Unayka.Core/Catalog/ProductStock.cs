using Unayka.Core.Locations;

namespace Unayka.Core.Catalog;

public static class ProductStock
{
    public const int Unlimited = int.MaxValue;

    /// <summary>Quantity available across active locations, always unlimited for digital products. Needs Stocks with Location loaded.</summary>
    public static int Total(Product product) =>
        product.Kind == ProductKind.Digital ? Unlimited : product.Stocks.Where(IsAtActiveLocation).Sum(stock => stock.Quantity);

    private static bool IsAtActiveLocation(LocationStock stock) => stock.Location?.Status == CatalogStatus.Active;
}
