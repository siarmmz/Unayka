using Unayka.Core.Catalog;
using Unayka.Core.Locations;

namespace Unayka.Core.Administration;

/// <summary>Row of the location list. CanDelete is false when an order references the location.</summary>
public sealed record LocationRow(int Id, string Name, string City, string Slug, CatalogStatus Status, int SortOrder, int ContactCount, bool CanDelete);

/// <summary>Editable contact channel of a location.</summary>
public sealed class LocationContactForm
{
    public ContactKind Kind { get; set; } = ContactKind.Phone;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Editable location with its contacts.</summary>
public sealed class LocationForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string OpeningHours { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string MapEmbedUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public List<LocationContactForm> Contacts { get; set; } = [];
}
