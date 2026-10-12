using Unayka.Core.Catalog;

namespace Unayka.Core.Administration;

/// <summary>Row of the section list. CanDelete is false when the section has categories.</summary>
public sealed record SectionRow(int Id, string Name, string Slug, CatalogStatus Status, int SortOrder, int CategoryCount, int ProductCount, string ImageUrl, bool CanDelete);

/// <summary>Row of the category list. CanDelete is false when the category has products.</summary>
public sealed record CategoryRow(int Id, string Name, string Slug, string SectionName, CatalogStatus Status, int ProductCount, int SortOrder, string ImageUrl, bool CanDelete);

/// <summary>Row of the tag list.</summary>
public sealed record TagRow(int Id, string Name, string Slug, string SectionName, int ProductCount, int SortOrder);

/// <summary>Editable section.</summary>
public sealed class SectionForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
}

/// <summary>Editable category.</summary>
public sealed class CategoryForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
}

/// <summary>Editable tag.</summary>
public sealed class TagForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public int SortOrder { get; set; }
}
