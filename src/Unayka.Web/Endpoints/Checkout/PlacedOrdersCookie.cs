using Microsoft.AspNetCore.DataProtection;

namespace Unayka.Web.Endpoints.Checkout;

public static class PlacedOrdersCookie
{
    public const string Name = "unayka_orders";

    private const string Purpose = "Unayka.PlacedOrders";
    private const char Separator = ',';
    private const int MaxRemembered = 20;
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static IReadOnlyCollection<string> Read(HttpRequest request, IDataProtectionProvider protection)
    {
        var value = request.Cookies[Name];
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        try
        {
            return protection.CreateProtector(Purpose).Unprotect(value).Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return [];
        }
    }

    public static void Append(HttpContext context, IDataProtectionProvider protection, string orderNumber)
    {
        var numbers = Read(context.Request, protection).Append(orderNumber).TakeLast(MaxRemembered);
        context.Response.Cookies.Append(
            Name,
            protection.CreateProtector(Purpose).Protect(string.Join(Separator, numbers)),
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                MaxAge = Lifetime,
                IsEssential = true,
            });
    }
}
