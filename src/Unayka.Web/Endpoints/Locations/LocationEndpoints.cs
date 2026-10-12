namespace Unayka.Web.Endpoints.Locations;

public static class LocationEndpoints
{
    public const string ContactUrl = "/contacto";

    private static readonly string[] LegacyRoutes = ["/contact", "/location", "/localizacao"];

    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        foreach (var route in LegacyRoutes)
        {
            endpoints.MapGet(route, () => Results.Redirect(ContactUrl, permanent: true));
        }

        return endpoints;
    }
}
