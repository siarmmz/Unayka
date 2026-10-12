using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Administration;

/// <summary>Audit entry with the name of the actor.</summary>
public sealed record AuditRow(int Id, DateTimeOffset At, string ActorName, string Action, string EntityType, string EntityId, string Summary);

public interface IAdminAudit
{
    /// <summary>Lists audit entries newest first.</summary>
    Task<PagedResult<AuditRow>> ListAsync(int page, CancellationToken cancellationToken = default);
}

public sealed class AdminAuditService(IDbContextFactory<UnaykaDbContext> factory) : IAdminAudit
{
    private const string SystemActor = "Sistema";

    public async Task<PagedResult<AuditRow>> ListAsync(int page, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var pageNumber = AdminPaging.NormalizePage(page);
        var total = await db.AuditEntries.CountAsync(cancellationToken);
        var entries = await db.AuditEntries.AsNoTracking()
            .OrderByDescending(entry => entry.At)
            .ThenByDescending(entry => entry.Id)
            .Skip((pageNumber - 1) * AdminPaging.DefaultPageSize)
            .Take(AdminPaging.DefaultPageSize)
            .ToListAsync(cancellationToken);
        var actorIds = entries.Where(entry => entry.ActorUserId != null).Select(entry => entry.ActorUserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);
        var rows = entries
            .Select(entry => new AuditRow(
                entry.Id,
                entry.At,
                entry.ActorUserId is { } actorId ? names.GetValueOrDefault(actorId, SystemActor) : SystemActor,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.Summary))
            .ToList();
        return new PagedResult<AuditRow>(rows, pageNumber, AdminPaging.DefaultPageSize, total);
    }
}
