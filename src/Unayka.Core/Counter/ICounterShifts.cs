namespace Unayka.Core.Counter;

/// <summary>Row of the closed shift history.</summary>
public sealed record ShiftHistoryRow(
    int Id,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    long OpeningMinor,
    long ExpectedMinor,
    long? CountedMinor);

/// <summary>Open shift with its sales figures.</summary>
public sealed record ShiftSummary(CashShift Shift, ShiftTotals Totals);

public interface ICounterShifts
{
    /// <summary>Returns the shift that is currently open, if any.</summary>
    Task<CashShift?> GetOpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a shift with the given opening amount, or returns the already open one.</summary>
    Task<CashShift> OpenAsync(int userId, long openingMinor, CancellationToken cancellationToken = default);

    /// <summary>Sums the paid counter payments registered since the shift was opened.</summary>
    Task<ShiftSummary> SummarizeAsync(CashShift shift, CancellationToken cancellationToken = default);

    /// <summary>Closes the open shift with the counted cash and returns the final summary.</summary>
    Task<ShiftSummary?> CloseAsync(int userId, long countedCashMinor, string? note, CancellationToken cancellationToken = default);

    /// <summary>Lists the most recent shifts, newest first.</summary>
    Task<IReadOnlyList<ShiftHistoryRow>> GetHistoryAsync(int count, CancellationToken cancellationToken = default);
}
