using Unayka.Core.Locations;

namespace Unayka.Core.Data.Seed;

public sealed record SeedContact(ContactKind Kind, string Label, string Value);

public sealed record SeedLocation(
    string Slug,
    string Name,
    string City,
    string Address,
    string OpeningHours,
    double Latitude,
    double Longitude,
    IReadOnlyList<SeedContact> Contacts)
{
    private const double MapMargin = 0.005;

    public string MapEmbedUrl =>
        FormattableString.Invariant(
            $"https://www.openstreetmap.org/export/embed.html?bbox={Longitude - MapMargin}%2C{Latitude - MapMargin}%2C{Longitude + MapMargin}%2C{Latitude + MapMargin}&layer=mapnik&marker={Latitude}%2C{Longitude}");
}

public static class LocationSeed
{
    private const string OpeningHours = "Segunda a sábado, das 8h às 18h";
    private const string CallLabel = "Chamadas";
    private const string WhatsAppLabel = "WhatsApp";
    private const string EmailLabel = "Email";

    public static readonly IReadOnlyList<SeedLocation> Locations =
    [
        new(
            "vilanculos",
            "Unayka Vilanculos",
            "Vilanculos",
            "Avenida da Marginal, Vilanculos, Inhambane",
            OpeningHours,
            -21.9810,
            35.3090,
            [
                new(ContactKind.Phone, CallLabel, "+258 84 000 0101"),
                new(ContactKind.WhatsApp, WhatsAppLabel, "+258 84 000 0101"),
                new(ContactKind.Email, EmailLabel, "vilanculos@unayka.co.mz"),
            ]),
        new(
            "maputo-polana",
            "Unayka Maputo, Polana",
            "Maputo",
            "Avenida Julius Nyerere, Polana, Maputo",
            OpeningHours,
            -25.9560,
            32.5920,
            [
                new(ContactKind.Phone, CallLabel, "+258 84 000 0201"),
                new(ContactKind.WhatsApp, WhatsAppLabel, "+258 82 000 0202"),
                new(ContactKind.Email, EmailLabel, "maputo@unayka.co.mz"),
            ]),
    ];
}
