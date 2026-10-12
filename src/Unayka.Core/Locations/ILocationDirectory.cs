namespace Unayka.Core.Locations;

/// <summary>Contact channel shown for a location.</summary>
public sealed record LocationContactSummary(ContactKind Kind, string Label, string Value);

/// <summary>Active location with its contacts in sort order.</summary>
public sealed record LocationSummary(
    int Id,
    string Slug,
    string Name,
    string City,
    string Address,
    string OpeningHours,
    double? Latitude,
    double? Longitude,
    string MapEmbedUrl,
    IReadOnlyList<LocationContactSummary> Contacts);

/// <summary>Quantity of one product at one active location.</summary>
public sealed record LocationProductStock(int LocationId, string LocationName, int Quantity);

public interface ILocationDirectory
{
    /// <summary>Active locations in sort order, each with its contacts in sort order.</summary>
    Task<IReadOnlyList<LocationSummary>> ActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Quantity of a product at every active location in sort order.</summary>
    Task<IReadOnlyList<LocationProductStock>> StockForProductAsync(int productId, CancellationToken cancellationToken = default);
}
