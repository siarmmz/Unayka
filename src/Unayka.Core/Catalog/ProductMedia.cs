namespace Unayka.Core.Catalog;

/// <summary>Image of a product. The first by SortOrder is the card image.</summary>
public sealed class ProductMedia
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public ProductMediaKind Kind { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
