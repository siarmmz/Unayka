using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Counter;

// Temporary: the primary agent must replace this with IOrderNumberGenerator (Unayka.Core.Orders).
internal static class CounterOrderNumbers
{
    private const string Prefix = "UN-";
    private const int Digits = 6;

    public static async Task<string> NextAsync(UnaykaDbContext database, CancellationToken cancellationToken)
    {
        var numbers = await database.Orders.Select(order => order.Number).ToListAsync(cancellationToken);
        var highest = numbers
            .Where(number => number.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(number => int.TryParse(number[Prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{Prefix}{(highest + 1).ToString($"D{Digits}", CultureInfo.InvariantCulture)}";
    }
}
