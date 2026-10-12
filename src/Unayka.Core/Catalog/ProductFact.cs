namespace Unayka.Core.Catalog;

/// <summary>One row of a product information table, such as nutrition or composition.</summary>
public sealed class ProductFact
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
