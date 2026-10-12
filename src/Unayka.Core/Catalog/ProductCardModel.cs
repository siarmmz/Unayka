using System.Linq.Expressions;

namespace Unayka.Core.Catalog;

/// <summary>Data shown on a product card in listings. ImageUrls are ordered for the card cross fade.</summary>
public sealed record ProductCardModel(
    int Id,
    string Slug,
    string Name,
    string CategoryName,
    long PriceMinor,
    long? CompareAtPriceMinor,
    string ImageUrl,
    IReadOnlyList<string> ImageUrls,
    string? Badge,
    bool IsNew,
    ProductKind Kind,
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
        product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).ToList(),
        product.Badge,
        product.IsNew,
        product.Kind,
        product.Kind == ProductKind.Digital
        || product.Stocks.Any(stock => stock.Quantity > 0 && stock.Location!.Status == CatalogStatus.Active));
}
