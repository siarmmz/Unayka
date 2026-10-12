using System.Linq.Expressions;

namespace Unayka.Core.Catalog;

public static class ProductVisibility
{
    /// <summary>Product that customers may see and buy: active, in an active category of an active section.</summary>
    public static readonly Expression<Func<Product, bool>> IsPublic = product =>
        product.Status == CatalogStatus.Active
        && product.Category!.Status == CatalogStatus.Active
        && product.Category.Section!.Status == CatalogStatus.Active;
}
