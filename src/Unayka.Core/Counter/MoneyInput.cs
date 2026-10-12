using System.Globalization;

namespace Unayka.Core.Counter;

public static class MoneyInput
{
    private const int MinorUnitsPerUnit = 100;
    private const decimal MaximumAmount = 10_000_000m;

    public static bool TryParse(string? text, out long minorUnits)
    {
        minorUnits = 0;
        var normalized = (text ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount < 0 || amount > MaximumAmount)
        {
            return false;
        }

        minorUnits = (long)Math.Round(amount * MinorUnitsPerUnit, MidpointRounding.AwayFromZero);
        return true;
    }

    public static long ParseOrZero(string? text) => TryParse(text, out var minor) ? minor : 0;
}
