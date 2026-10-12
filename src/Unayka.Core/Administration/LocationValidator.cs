using System.Net.Mail;
using Unayka.Core.Locations;

namespace Unayka.Core.Administration;

public static class LocationValidator
{
    private const double MaxLatitude = 90;
    private const double MaxLongitude = 180;
    private const char LandlinePrefixDigit = '2';
    private const char MobilePrefixDigit = '8';
    private const string CountryCode = "258";
    private const int NationalLength = 9;

    public static List<string> Validate(LocationForm form)
    {
        var errors = new List<string>();
        AddIf(errors, string.IsNullOrWhiteSpace(form.Name), "Indique o nome da loja.");
        AddIf(errors, !Slugs.IsValid(form.Slug), "O endereço (slug) só pode ter letras minúsculas, números e hífens.");
        AddIf(errors, (form.Latitude is null) != (form.Longitude is null), "Indique a latitude e a longitude em conjunto.");
        AddIf(errors, form.Latitude is { } latitude && Math.Abs(latitude) > MaxLatitude, "A latitude deve estar entre -90 e 90.");
        AddIf(errors, form.Longitude is { } longitude && Math.Abs(longitude) > MaxLongitude, "A longitude deve estar entre -180 e 180.");
        AddIf(errors, !string.IsNullOrWhiteSpace(form.MapEmbedUrl) && !IsSecureUrl(form.MapEmbedUrl), "O endereço do mapa deve começar por https.");
        foreach (var contact in form.Contacts)
        {
            AddIf(errors, string.IsNullOrWhiteSpace(contact.Label), "Cada contacto precisa de uma etiqueta.");
            AddIf(errors, !IsValidContact(contact), InvalidContactMessage(contact));
        }

        return errors.Distinct().ToList();
    }

    public static string NormalizeContactValue(LocationContactForm contact) =>
        contact.Kind == ContactKind.Email ? contact.Value.Trim().ToLowerInvariant() : FormatPhone(NationalDigits(contact.Value) ?? contact.Value.Trim());

    private static bool IsValidContact(LocationContactForm contact) =>
        contact.Kind == ContactKind.Email ? IsEmail(contact.Value) : NationalDigits(contact.Value) is not null;

    private static string InvalidContactMessage(LocationContactForm contact) =>
        contact.Kind == ContactKind.Email ? "O email indicado não é válido." : "O número deve ter 9 dígitos, com ou sem +258.";

    private static string? NationalDigits(string value)
    {
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith(CountryCode, StringComparison.Ordinal) && digits.Length == CountryCode.Length + NationalLength)
        {
            digits = digits[CountryCode.Length..];
        }

        var validPrefix = digits.Length > 0 && (digits[0] == LandlinePrefixDigit || digits[0] == MobilePrefixDigit);
        return digits.Length == NationalLength && validPrefix ? digits : null;
    }

    private static string FormatPhone(string value) =>
        value.Length == NationalLength && value.All(char.IsAsciiDigit)
            ? $"+{CountryCode} {value[..2]} {value[2..5]} {value[5..]}"
            : value;

    private static bool IsEmail(string value) =>
        MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim() && address.Host.Contains('.', StringComparison.Ordinal);

    private static bool IsSecureUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static void AddIf(List<string> errors, bool condition, string message)
    {
        if (condition)
        {
            errors.Add(message);
        }
    }
}
