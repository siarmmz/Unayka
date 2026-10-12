using Unayka.Core.Orders.Downloads;
using Unayka.Web.Endpoints.Cart;

namespace Unayka.Web.Endpoints.Downloads;

public static class DownloadEndpoints
{
    private const string Route = "/conta/descargas/{orderItemId:int}";

    public static string UrlFor(int orderItemId) => $"/conta/descargas/{orderItemId}";

    public static IEndpointRouteBuilder MapDownloadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, (Delegate)DownloadAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> DownloadAsync(int orderItemId, HttpContext context, IDigitalDownloads downloads)
    {
        var result = await downloads.OpenAsync(orderItemId, CartCookies.ReadUserId(context.User), context.RequestAborted);
        if (result.File is null)
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "private, no-store";
        return Results.File(result.File.Content, result.File.ContentType, result.File.FileName);
    }
}
