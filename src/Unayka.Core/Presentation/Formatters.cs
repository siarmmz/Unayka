using System.Globalization;
using System.Text;

namespace Unayka.Core.Presentation;

public static class Formatters
{
    private const int MinorUnitsPerUnit = 100;
    private const string CurrencyCode = "MZN";
    private const string CountryCode = "258";
    private const int LocalNumberLength = 9;
    private const string NonBreakingSpace = "\u00A0";
    private static readonly TimeSpan MaputoOffset = TimeSpan.FromHours(2);

    public static string FormatMoney(long minorUnits)
    {
        var amount = minorUnits / (decimal)MinorUnitsPerUnit;
        var text = amount.ToString("#,##0.00", CultureInfo.InvariantCulture)
            .Replace(",", NonBreakingSpace, StringComparison.Ordinal)
            .Replace(".", ",", StringComparison.Ordinal);
        return $"{text}{NonBreakingSpace}{CurrencyCode}";
    }

    public static string FormatDate(DateTimeOffset value) =>
        value.ToOffset(MaputoOffset).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string FormatPhone(string? value)
    {
        var digits = new StringBuilder();
        foreach (var character in value ?? string.Empty)
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
        }

        var number = digits.ToString();
        if (number.StartsWith(CountryCode, StringComparison.Ordinal) && number.Length == CountryCode.Length + LocalNumberLength)
        {
            number = number[CountryCode.Length..];
        }

        if (number.Length != LocalNumberLength)
        {
            return value ?? string.Empty;
        }

        return $"+{CountryCode} {number[..2]} {number[2..5]} {number[5..]}";
    }
}
