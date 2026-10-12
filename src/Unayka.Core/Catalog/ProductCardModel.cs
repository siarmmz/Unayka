using System.Linq.Expressions;

namespace Unayka.Core.Catalog;

/// <summary>Data shown on a product card in listings.</summary>
public sealed record ProductCardModel(
    string Slug,
    string Name,
    string CategoryName,
    long PriceMinor,
    long? CompareAtPriceMinor,
    string ImageUrl,
    string? Badge)
{
    public static readonly Expression<Func<Product, ProductCardModel>> FromProduct = product => new ProductCardModel(
        product.Slug,
        product.Name,
        product.Category!.Name,
        product.PriceMinor,
        product.CompareAtPriceMinor,
        product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault() ?? string.Empty,
        product.Badge);
}
