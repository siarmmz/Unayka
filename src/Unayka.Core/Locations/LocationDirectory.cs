using Microsoft.EntityFrameworkCore;
using Unayka.Core.Catalog;
using Unayka.Core.Data;

namespace Unayka.Core.Locations;

public sealed class LocationDirectory(UnaykaDbContext database) : ILocationDirectory
{
    public async Task<IReadOnlyList<LocationSummary>> ActiveAsync(CancellationToken cancellationToken = default)
    {
        var locations = await database.Locations.AsNoTracking()
            .Include(location => location.Contacts)
            .Where(location => location.Status == CatalogStatus.Active)
            .OrderBy(location => location.SortOrder)
            .ThenBy(location => location.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return locations.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<LocationProductStock>> StockForProductAsync(int productId, CancellationToken cancellationToken = default) =>
        await database.LocationStocks.AsNoTracking()
            .Where(stock => stock.ProductId == productId && stock.Location!.Status == CatalogStatus.Active)
            .OrderBy(stock => stock.Location!.SortOrder)
            .ThenBy(stock => stock.Location!.Name)
            .Select(stock => new LocationProductStock(stock.LocationId, stock.Location!.Name, stock.Quantity))
            .ToListAsync(cancellationToken);

    private static LocationSummary ToSummary(Location location) => new(
        location.Id,
        location.Slug,
        location.Name,
        location.City,
        location.Address,
        location.OpeningHours,
        location.Latitude,
        location.Longitude,
        location.MapEmbedUrl,
        location.Contacts
            .OrderBy(contact => contact.SortOrder)
            .Select(contact => new LocationContactSummary(contact.Kind, contact.Label, contact.Value))
            .ToList());
}
