namespace Unayka.Core.Counter;

/// <summary>Line of a counter ticket. ProductId is zero for a free amount line.</summary>
public sealed record TicketLine(int ProductId, string Name, long UnitPriceMinor, int Quantity)
{
    public const int FreeAmountProductId = 0;

    public long TotalMinor => UnitPriceMinor * Quantity;

    public bool IsFreeAmount => ProductId == FreeAmountProductId;
}

public static class Ticket
{
    public static long Total(IEnumerable<TicketLine> lines) => lines.Sum(line => line.TotalMinor);
}
