using System.Security.Claims;
using Unayka.Core.Carts;

namespace Unayka.Web.Endpoints.Cart;

public static class CartCookies
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static Guid? Read(HttpRequest request) =>
        Guid.TryParse(request.Cookies[CartCookie.Name], out var id) ? id : null;

    public static void Write(HttpContext context, Guid cartId) =>
        context.Response.Cookies.Append(CartCookie.Name, cartId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            MaxAge = Lifetime,
            IsEssential = true,
        });

    public static int? ReadUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
