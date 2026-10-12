using Unayka.Core.Orders;

namespace Unayka.Core.Counter;

/// <summary>Card of the preparation board.</summary>
public sealed record PreparationCard(
    int OrderId,
    string Number,
    string CustomerName,
    OrderChannel Channel,
    OrderStatus Status,
    IReadOnlyList<string> Items,
    int AgeMinutes);

/// <summary>Orders grouped by preparation column.</summary>
public sealed record PreparationBoard(
    IReadOnlyList<PreparationCard> Incoming,
    IReadOnlyList<PreparationCard> Preparing,
    IReadOnlyList<PreparationCard> Ready,
    IReadOnlyList<PreparationCard> DeliveredToday);

public interface ICounterPreparation
{
    /// <summary>Loads the current board with online and counter orders.</summary>
    Task<PreparationBoard> LoadBoardAsync(CancellationToken cancellationToken = default);

    /// <summary>Moves the order to its next status and writes an audit entry. Returns false when it cannot advance.</summary>
    Task<bool> AdvanceAsync(int orderId, int userId, CancellationToken cancellationToken = default);
}
