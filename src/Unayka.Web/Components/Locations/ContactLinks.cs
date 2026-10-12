using Unayka.Core.Accounts;
using Unayka.Core.Locations;

namespace Unayka.Web.Components.Locations;

public sealed record ContactLink(string Label, string Display, string Url);

public static class ContactLinks
{
    public const string MapHost = "https://www.openstreetmap.org/";

    private const string CountryCode = "258";
    private const string TelPrefix = "tel:+";
    private const string WhatsAppPrefix = "https://wa.me/";
    private const string MailPrefix = "mailto:";

    public static ContactLink For(LocationContactSummary contact) =>
        new(contact.Label, contact.Value, contact.Kind switch
        {
            ContactKind.Phone => TelPrefix + InternationalDigits(contact.Value),
            ContactKind.WhatsApp => WhatsAppPrefix + InternationalDigits(contact.Value),
            _ => MailPrefix + contact.Value.Trim(),
        });

    public static string? SafeMapUrl(string? mapEmbedUrl) =>
        mapEmbedUrl is not null && mapEmbedUrl.StartsWith(MapHost, StringComparison.Ordinal) ? mapEmbedUrl : null;

    private static string InternationalDigits(string value)
    {
        var national = PhoneNumber.Normalize(value);
        return national is null ? new string(value.Where(char.IsAsciiDigit).ToArray()) : CountryCode + national;
    }
}
