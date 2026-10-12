namespace Unayka.Core.Counter;

/// <summary>Cash drawer period opened and closed by a cashier. Amounts are in minor units.</summary>
public sealed class CashShift
{
    public int Id { get; set; }
    public int OpenedByUserId { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public long OpeningMinor { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public long? CountedMinor { get; set; }
    public long? ExpectedMinor { get; set; }
}
