using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unayka.Core.Catalog;
using Unayka.Core.Data;

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

public enum ContactKind
{
    Phone,
    WhatsApp,
    Email,
}

/// <summary>Contact channel of a location.</summary>
public sealed class LocationContact
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public ContactKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Quantity of a physical product available at one location.</summary>
public sealed class LocationStock
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int LocationId { get; set; }
    public Location? Location { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Contact channel shown for a location.</summary>
public sealed record LocationContactSummary(ContactKind Kind, string Label, string Value)
{
    private const string CountryCode = "258";
    private const string TelPrefix = "tel:+";
    private const string WhatsAppPrefix = "https://wa.me/";
    private const string MailPrefix = "mailto:";

    public string Href => Kind switch
    {
        ContactKind.Phone => TelPrefix + InternationalDigits(Value),
        ContactKind.WhatsApp => WhatsAppPrefix + InternationalDigits(Value),
        _ => MailPrefix + Value.Trim(),
    };

    public string Icon => Kind switch
    {
        ContactKind.Phone => "bi-telephone",
        ContactKind.WhatsApp => "bi-whatsapp",
        _ => "bi-envelope",
    };

    private static string InternationalDigits(string value)
    {
        var national = Accounts.PhoneNumber.Normalize(value);
        return national is null ? new string(value.Where(char.IsAsciiDigit).ToArray()) : CountryCode + national;
    }
}

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
    IReadOnlyList<LocationContactSummary> Contacts)
{
    public const string MapHost = "https://www.openstreetmap.org/";

    public string? SafeMapUrl => MapEmbedUrl.StartsWith(MapHost, StringComparison.Ordinal) ? MapEmbedUrl : null;
}

/// <summary>Quantity of one product at one active location.</summary>
public sealed record LocationProductStock(int LocationId, string LocationName, int Quantity);

public interface ILocationDirectory
{
    /// <summary>Active locations in sort order, each with its contacts in sort order.</summary>
    Task<IReadOnlyList<LocationSummary>> ActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Quantity of a product at every active location in sort order.</summary>
    Task<IReadOnlyList<LocationProductStock>> StockForProductAsync(int productId, CancellationToken cancellationToken = default);
}

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

/// <summary>Quantity taken from one location for an order line.</summary>
public sealed record StockAllocation(int LocationId, int Quantity);

public static class StockAllocator
{
    /// <summary>Takes the quantity from the first location that holds all of it, otherwise from the locations in order. Null when the total is not enough.</summary>
    public static IReadOnlyList<StockAllocation>? Allocate(IReadOnlyList<LocationStock> stocksInLocationOrder, int quantity)
    {
        if (stocksInLocationOrder.FirstOrDefault(stock => stock.Quantity >= quantity) is { } single)
        {
            return [new StockAllocation(single.LocationId, quantity)];
        }

        var allocations = new List<StockAllocation>();
        var remaining = quantity;
        foreach (var stock in stocksInLocationOrder.Where(stock => stock.Quantity > 0))
        {
            var taken = Math.Min(stock.Quantity, remaining);
            allocations.Add(new StockAllocation(stock.LocationId, taken));
            remaining -= taken;
        }

        return remaining == 0 ? allocations : null;
    }
}

public static class LocationServiceExtensions
{
    public static IServiceCollection AddUnaykaLocations(this IServiceCollection services)
    {
        services.AddScoped<ILocationDirectory, LocationDirectory>();
        return services;
    }
}
