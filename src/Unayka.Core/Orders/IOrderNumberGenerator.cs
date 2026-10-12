namespace Unayka.Core.Orders;

public interface IOrderNumberGenerator
{
    /// <summary>Returns the next order number in the format UN-000123.</summary>
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
