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
