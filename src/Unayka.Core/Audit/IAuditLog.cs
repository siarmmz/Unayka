namespace Unayka.Core.Audit;

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
