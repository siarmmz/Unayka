using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Orders;

public sealed class OrderNumberGenerator(UnaykaDbContext database) : IOrderNumberGenerator
{
    private const string Prefix = "UN-";
    private const string Format = "D6";

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var last = await database.Orders.AsNoTracking()
            .Where(order => order.Number.StartsWith(Prefix))
            .OrderByDescending(order => order.Number)
            .Select(order => order.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var lastValue = last is not null && int.TryParse(last.AsSpan(Prefix.Length), CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        return Prefix + (lastValue + 1).ToString(Format, CultureInfo.InvariantCulture);
    }
}
