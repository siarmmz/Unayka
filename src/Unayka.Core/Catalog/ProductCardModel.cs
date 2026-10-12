using System.Linq.Expressions;

namespace Unayka.Core.Catalog;

/// <summary>Data shown on a product card in listings.</summary>
public sealed record ProductCardModel(
    int Id,
    string Slug,
    string Name,
    string CategoryName,
    long PriceMinor,
    long? CompareAtPriceMinor,
    string ImageUrl,
    string? Badge,
    bool InStock)
{
    public static readonly Expression<Func<Product, ProductCardModel>> FromProduct = product => new ProductCardModel(
        product.Id,
        product.Slug,
        product.Name,
        product.Category!.Name,
        product.PriceMinor,
        product.CompareAtPriceMinor,
        product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault() ?? string.Empty,
        product.Badge,
        product.StockQuantity > 0);
}
