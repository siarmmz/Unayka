using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Carts;

public sealed class CartSummary(UnaykaDbContext database) : ICartSummary
{
    public async Task<int> CountItemsAsync(Guid? cartId, CancellationToken cancellationToken = default)
    {
        if (cartId is null)
        {
            return 0;
        }

        return await database.CartItems
            .Where(item => item.CartId == cartId)
            .SumAsync(item => item.Quantity, cancellationToken);
    }
}
