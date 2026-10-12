namespace Unayka.Core.Catalog;

/// <summary>Free label scoped to a section, used to filter products.</summary>
public sealed class Tag
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public Section? Section { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<Product> Products { get; set; } = [];
}
