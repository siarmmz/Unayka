using System.Globalization;
using System.Text.Json;
using Unayka.Core.Catalog;
using Unayka.Core.Configuration;

namespace Unayka.Web.Components.Store;

public static class StoreStructuredData
{
    private const string Context = "https://schema.org";
    private const string Currency = "MZN";
    private const string InStockUrl = "https://schema.org/InStock";
    private const string OutOfStockUrl = "https://schema.org/OutOfStock";
    private const string BrandName = "Unayka";
    private const decimal MinorUnitsPerUnit = 100m;

    private static readonly string[] StoreTypes = ["Store", "CafeOrCoffeeShop"];

    public static string Root(string navigationBaseUri)
    {
        var configured = AppEnvConfig.PublicBaseUrl;
        return (string.IsNullOrEmpty(configured) ? navigationBaseUri : configured).TrimEnd('/');
    }

    public static string Product(ProductDetail product, string root) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@context"] = Context,
            ["@type"] = "Product",
            ["name"] = product.Name,
            ["description"] = product.ShortDescription,
            ["sku"] = product.Slug,
            ["category"] = product.CategoryName,
            ["image"] = product.Images.Select(image => image.Url).ToArray(),
            ["brand"] = new Dictionary<string, object> { ["@type"] = "Brand", ["name"] = BrandName },
            ["offers"] = new Dictionary<string, object>
            {
                ["@type"] = "Offer",
                ["url"] = root + StoreUrls.Product(product.Slug),
                ["priceCurrency"] = Currency,
                ["price"] = (product.PriceMinor / MinorUnitsPerUnit).ToString("0.00", CultureInfo.InvariantCulture),
                ["availability"] = product.StockQuantity > 0 ? InStockUrl : OutOfStockUrl,
            },
        });

    public static string Store(string root, string imageUrl, string description, string[] openingHours) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@context"] = Context,
            ["@type"] = StoreTypes,
            ["name"] = BrandName,
            ["description"] = description,
            ["url"] = root + "/",
            ["image"] = imageUrl,
            ["currenciesAccepted"] = Currency,
            ["paymentAccepted"] = "M-Pesa, e-Mola, mKesh",
            ["openingHours"] = openingHours,
            ["address"] = new Dictionary<string, object>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = "Avenida 24 de Julho",
                ["addressLocality"] = "Maputo",
                ["addressCountry"] = "MZ",
            },
        });
}
