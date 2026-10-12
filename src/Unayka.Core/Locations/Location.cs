using Unayka.Core.Catalog;

namespace Unayka.Core.Locations;

/// <summary>Physical point where products are stocked and picked up.</summary>
public sealed class Location
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string OpeningHours { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string MapEmbedUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public List<LocationContact> Contacts { get; set; } = [];
}
