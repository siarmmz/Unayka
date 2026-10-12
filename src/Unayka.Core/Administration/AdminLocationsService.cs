using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;

namespace Unayka.Core.Administration;

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
