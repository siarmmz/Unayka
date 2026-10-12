namespace Unayka.Core.Catalog;

/// <summary>Group of products shown together in the shop.</summary>
public sealed class Category
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<Product> Products { get; set; } = [];
}
