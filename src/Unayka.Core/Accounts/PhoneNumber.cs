namespace Unayka.Core.Accounts;

public static class PhoneNumber
{
    private const string CountryCode = "258";
    private const int NationalLength = 9;
    private const char MobilePrefix = '8';

    public static string? Normalize(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith(CountryCode, StringComparison.Ordinal) && digits.Length == CountryCode.Length + NationalLength)
        {
            digits = digits[CountryCode.Length..];
        }

        return digits.Length == NationalLength && digits[0] == MobilePrefix ? digits : null;
    }
}
