using Unayka.Core.Data;

namespace Unayka.Core.Audit;

public sealed class AuditLog(UnaykaDbContext database, TimeProvider time) : IAuditLog
{
    public async Task WriteAsync(
        int? actorUserId,
        string action,
        string entityType,
        string entityId,
        string summary,
        CancellationToken cancellationToken = default)
    {
        database.AuditEntries.Add(new AuditEntry
        {
            At = time.GetUtcNow(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
