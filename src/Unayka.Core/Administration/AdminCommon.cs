using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Orders;

namespace Unayka.Core.Administration;

/// <summary>One page of a larger result set.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>Outcome of an administration command with user facing messages.</summary>
public sealed record AdminResult(bool Succeeded, IReadOnlyList<string> Errors, int? EntityId = null)
{
    public static AdminResult Ok(int? entityId = null) => new(true, [], entityId);

    public static AdminResult Fail(params string[] errors) => new(false, errors);

    public static AdminResult Fail(IReadOnlyList<string> errors) => new(false, errors);
}

public static class SearchPattern
{
    public static string? Contains(string? term)
    {
        var cleaned = (term ?? string.Empty).Replace("%", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).Trim();
        return cleaned.Length == 0 ? null : $"%{cleaned}%";
    }
}

public static class AdminPaging
{
    public const int DefaultPageSize = 15;

    public static int NormalizePage(int page) => Math.Max(1, page);
}

public static class AdminOrderGroups
{
    public static readonly IReadOnlyList<OrderStatus> Revenue =
        [OrderStatus.Paid, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Delivered];

    public static readonly IReadOnlyList<OrderStatus> Open =
        [OrderStatus.New, OrderStatus.AwaitingPayment, OrderStatus.Paid, OrderStatus.Preparing, OrderStatus.Ready];
}

public static class LocalDays
{
    private static readonly TimeSpan MaputoOffset = TimeSpan.FromHours(2);

    public static DateOnly ToLocalDay(DateTimeOffset value) =>
        DateOnly.FromDateTime(value.ToOffset(MaputoOffset).DateTime);

    public static string FormatDateTime(DateTimeOffset value) =>
        value.ToOffset(MaputoOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static DateTimeOffset StartOfDay(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), MaputoOffset);
}

public static class MoneyInput
{
    private const int MinorUnitsPerUnit = 100;
    private const decimal MaximumAmount = 10_000_000m;

    public static long ToMinor(decimal amount) =>
        (long)Math.Round(amount * MinorUnitsPerUnit, MidpointRounding.AwayFromZero);

    public static decimal ToAmount(long minorUnits) => minorUnits / (decimal)MinorUnitsPerUnit;

    public static bool TryParse(string? text, out long minorUnits)
    {
        minorUnits = 0;
        var normalized = (text ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount < 0 || amount > MaximumAmount)
        {
            return false;
        }

        minorUnits = ToMinor(amount);
        return true;
    }

    public static long ParseOrZero(string? text) => TryParse(text, out var minor) ? minor : 0;
}

public static partial class Slugs
{
    public const int MaxLength = 80;

    public static string FromName(string? name)
    {
        var decomposed = (name ?? string.Empty).Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder();
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(char.ToLowerInvariant(character));
            }
        }

        var slug = NonSlugCharacters().Replace(plain.ToString(), "-").Trim('-');
        return slug.Length > MaxLength ? slug[..MaxLength].Trim('-') : slug;
    }

    public static bool IsValid(string? slug) => !string.IsNullOrEmpty(slug) && slug.Length <= MaxLength && ValidSlug().IsMatch(slug);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidSlug();
}

public static class AdminAuditActions
{
    public const string ProductCreated = "product.created";
    public const string ProductUpdated = "product.updated";
    public const string CategoryCreated = "category.created";
    public const string CategoryUpdated = "category.updated";
    public const string ProductDeleted = "product.deleted";
    public const string ProductStatusChanged = "product.status_changed";
    public const string ProductFileChanged = "product.file_changed";
    public const string CategoryDeleted = "category.deleted";
    public const string CategoryStatusChanged = "category.status_changed";
    public const string SectionCreated = "section.created";
    public const string SectionUpdated = "section.updated";
    public const string SectionDeleted = "section.deleted";
    public const string SectionStatusChanged = "section.status_changed";
    public const string TagCreated = "tag.created";
    public const string TagUpdated = "tag.updated";
    public const string TagDeleted = "tag.deleted";
    public const string LocationCreated = "location.created";
    public const string LocationUpdated = "location.updated";
    public const string LocationDeleted = "location.deleted";
    public const string LocationStatusChanged = "location.status_changed";
    public const string CouponCreated = "coupon.created";
    public const string CouponUpdated = "coupon.updated";
    public const string CouponToggled = "coupon.toggled";
    public const string OrderStatusChanged = "order.status_changed";
}

public static class AdminEntityTypes
{
    public const string Product = "Product";
    public const string Category = "Category";
    public const string Section = "Section";
    public const string Tag = "Tag";
    public const string Location = "Location";
    public const string Coupon = "Coupon";
    public const string Order = "Order";
}

public static class StatusNames
{
    public static string For(CatalogStatus status) => status switch
    {
        CatalogStatus.Active => "ativo",
        CatalogStatus.Inactive => "inativo",
        _ => "arquivado",
    };
}

public static class AuditLogExtensions
{
    public static Task WriteEntityAsync(this IAuditLog audit, int? actorUserId, string action, string entityType, int entityId, string summary, CancellationToken cancellationToken = default) =>
        audit.WriteAsync(actorUserId, action, entityType, entityId.ToString(CultureInfo.InvariantCulture), summary, cancellationToken);
}

public static class AdministrationServiceExtensions
{
    public static IServiceCollection AddUnaykaAdministration(this IServiceCollection services)
    {
        services.AddScoped<IAdminDashboard, AdminDashboardService>();
        services.AddScoped<IAdminOrders, AdminOrdersService>();
        services.AddScoped<IAdminCatalog, AdminCatalogService>();
        services.AddScoped<IAdminTaxonomy, AdminTaxonomyService>();
        services.AddScoped<IAdminLocations, AdminLocationsService>();
        services.AddScoped<IAdminCoupons, AdminCouponsService>();
        services.AddScoped<IAdminCustomers, AdminCustomersService>();
        services.AddScoped<IAdminPayments, AdminPaymentsService>();
        services.AddScoped<IAdminAudit, AdminAuditService>();
        return services;
    }
}

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

public static class LocationValidator
{
    private const double MaxLatitude = 90;
    private const double MaxLongitude = 180;
    private const char LandlinePrefixDigit = '2';
    private const char MobilePrefixDigit = '8';
    private const string CountryCode = "258";
    private const int NationalLength = 9;

    public static List<string> Validate(LocationForm form)
    {
        var errors = new List<string>();
        AddIf(errors, string.IsNullOrWhiteSpace(form.Name), "Indique o nome da loja.");
        AddIf(errors, !Slugs.IsValid(form.Slug), "O endereço (slug) só pode ter letras minúsculas, números e hífens.");
        AddIf(errors, (form.Latitude is null) != (form.Longitude is null), "Indique a latitude e a longitude em conjunto.");
        AddIf(errors, form.Latitude is { } latitude && Math.Abs(latitude) > MaxLatitude, "A latitude deve estar entre -90 e 90.");
        AddIf(errors, form.Longitude is { } longitude && Math.Abs(longitude) > MaxLongitude, "A longitude deve estar entre -180 e 180.");
        AddIf(errors, !string.IsNullOrWhiteSpace(form.MapEmbedUrl) && !IsSecureUrl(form.MapEmbedUrl), "O endereço do mapa deve começar por https.");
        foreach (var contact in form.Contacts)
        {
            AddIf(errors, string.IsNullOrWhiteSpace(contact.Label), "Cada contacto precisa de uma etiqueta.");
            AddIf(errors, !IsValidContact(contact), InvalidContactMessage(contact));
        }

        return errors.Distinct().ToList();
    }

    public static string NormalizeContactValue(LocationContactForm contact) =>
        contact.Kind == ContactKind.Email ? contact.Value.Trim().ToLowerInvariant() : FormatPhone(NationalDigits(contact.Value) ?? contact.Value.Trim());

    private static bool IsValidContact(LocationContactForm contact) =>
        contact.Kind == ContactKind.Email ? IsEmail(contact.Value) : NationalDigits(contact.Value) is not null;

    private static string InvalidContactMessage(LocationContactForm contact) =>
        contact.Kind == ContactKind.Email ? "O email indicado não é válido." : "O número deve ter 9 dígitos, com ou sem +258.";

    private static string? NationalDigits(string value)
    {
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith(CountryCode, StringComparison.Ordinal) && digits.Length == CountryCode.Length + NationalLength)
        {
            digits = digits[CountryCode.Length..];
        }

        var validPrefix = digits.Length > 0 && (digits[0] == LandlinePrefixDigit || digits[0] == MobilePrefixDigit);
        return digits.Length == NationalLength && validPrefix ? digits : null;
    }

    private static string FormatPhone(string value) =>
        value.Length == NationalLength && value.All(char.IsAsciiDigit)
            ? $"+{CountryCode} {value[..2]} {value[2..5]} {value[5..]}"
            : value;

    private static bool IsEmail(string value) =>
        MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim() && address.Host.Contains('.', StringComparison.Ordinal);

    private static bool IsSecureUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static void AddIf(List<string> errors, bool condition, string message)
    {
        if (condition)
        {
            errors.Add(message);
        }
    }
}

public interface IAdminLocations
{
    /// <summary>Lists every location with its contact count.</summary>
    Task<IReadOnlyList<LocationRow>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads a location with its contacts for editing, or null when it does not exist.</summary>
    Task<LocationForm?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a location and its contacts after validation and audits the change.</summary>
    Task<AdminResult> SaveAsync(int? actorUserId, LocationForm form, CancellationToken cancellationToken = default);

    /// <summary>Changes the status of a location and audits the change.</summary>
    Task<AdminResult> SetStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default);

    /// <summary>Deletes a location that no order references, or fails telling it can only be archived.</summary>
    Task<AdminResult> DeleteAsync(int? actorUserId, int id, CancellationToken cancellationToken = default);
}

public sealed class AdminLocationsService(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit) : IAdminLocations
{
    private const string LocationNotFound = "Loja não encontrada.";

    public async Task<IReadOnlyList<LocationRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Locations.AsNoTracking()
            .OrderBy(location => location.SortOrder)
            .ThenBy(location => location.Name)
            .Select(location => new LocationRow(
                location.Id,
                location.Name,
                location.City,
                location.Slug,
                location.Status,
                location.SortOrder,
                location.Contacts.Count,
                !db.OrderItems.Any(item => item.LocationId == location.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<LocationForm?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var location = await db.Locations.AsNoTracking().Include(candidate => candidate.Contacts).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return location is null ? null : ToForm(location);
    }

    public async Task<AdminResult> SaveAsync(int? actorUserId, LocationForm form, CancellationToken cancellationToken = default)
    {
        form.Slug = form.Slug.Trim().ToLowerInvariant();
        var errors = LocationValidator.Validate(form);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.Locations.AnyAsync(location => location.Slug == form.Slug && location.Id != (form.Id ?? 0), cancellationToken))
        {
            errors.Add("Já existe uma loja com este endereço (slug).");
        }

        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        var location = form.Id is { } id ? await db.Locations.Include(candidate => candidate.Contacts).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken) : null;
        if (form.Id is not null && location is null)
        {
            return AdminResult.Fail(LocationNotFound);
        }

        var isNew = location is null;
        if (location is null)
        {
            location = new Location();
            db.Locations.Add(location);
        }

        Apply(form, location);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(
            actorUserId,
            isNew ? AdminAuditActions.LocationCreated : AdminAuditActions.LocationUpdated,
            AdminEntityTypes.Location,
            location.Id,
            isNew ? $"Loja {location.Name} criada." : $"Loja {location.Name} atualizada.",
            cancellationToken);
        return AdminResult.Ok(location.Id);
    }

    public async Task<AdminResult> SetStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var location = await db.Locations.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (location is null)
        {
            return AdminResult.Fail(LocationNotFound);
        }

        location.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.LocationStatusChanged, AdminEntityTypes.Location, id, $"Loja {location.Name}: {StatusNames.For(status)}.", cancellationToken);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> DeleteAsync(int? actorUserId, int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var location = await db.Locations.Include(candidate => candidate.Contacts).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (location is null)
        {
            return AdminResult.Fail(LocationNotFound);
        }

        if (await db.OrderItems.AnyAsync(item => item.LocationId == id, cancellationToken))
        {
            return AdminResult.Fail("Esta loja tem encomendas e só pode ser arquivada.");
        }

        await db.LocationStocks.Where(stock => stock.LocationId == id).ExecuteDeleteAsync(cancellationToken);
        db.Locations.Remove(location);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.LocationDeleted, AdminEntityTypes.Location, id, $"Loja {location.Name} eliminada.", cancellationToken);
        return AdminResult.Ok(id);
    }

    private static void Apply(LocationForm form, Location location)
    {
        location.Name = form.Name.Trim();
        location.Slug = form.Slug;
        location.City = form.City.Trim();
        location.Address = form.Address.Trim();
        location.OpeningHours = form.OpeningHours.Trim();
        location.Latitude = form.Latitude;
        location.Longitude = form.Longitude;
        location.MapEmbedUrl = form.MapEmbedUrl.Trim();
        location.SortOrder = form.SortOrder;
        location.Status = form.Status;
        location.Contacts.Clear();
        location.Contacts.AddRange(form.Contacts.Select(contact => new LocationContact
        {
            Kind = contact.Kind,
            Label = contact.Label.Trim(),
            Value = LocationValidator.NormalizeContactValue(contact),
            SortOrder = contact.SortOrder,
        }));
    }

    private static LocationForm ToForm(Location location) => new()
    {
        Id = location.Id,
        Name = location.Name,
        Slug = location.Slug,
        City = location.City,
        Address = location.Address,
        OpeningHours = location.OpeningHours,
        Latitude = location.Latitude,
        Longitude = location.Longitude,
        MapEmbedUrl = location.MapEmbedUrl,
        SortOrder = location.SortOrder,
        Status = location.Status,
        Contacts = location.Contacts.OrderBy(contact => contact.SortOrder)
            .Select(contact => new LocationContactForm { Kind = contact.Kind, Label = contact.Label, Value = contact.Value, SortOrder = contact.SortOrder }).ToList(),
    };
}
