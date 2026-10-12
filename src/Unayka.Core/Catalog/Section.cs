namespace Unayka.Core.Catalog;

/// <summary>Top level of the catalog, such as teas, cosmetics or books.</summary>
public sealed class Section
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public List<Category> Categories { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
}
