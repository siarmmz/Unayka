using Unayka.Core.Data;

namespace Unayka.Core.Audit;

/// <summary>Immutable record of a relevant action. It can only be inserted.</summary>
public sealed class AuditEntry
{
    public int Id { get; set; }
    public DateTimeOffset At { get; set; }
    public int? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public interface IAuditLog
{
    /// <summary>Appends one immutable entry to the audit log.</summary>
    Task WriteAsync(
        int? actorUserId,
        string action,
        string entityType,
        string entityId,
        string summary,
        CancellationToken cancellationToken = default);
}

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
