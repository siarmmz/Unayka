using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Configuration;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Web.Endpoints;

public static class CartEndpoints
{
    public const string NoticeKey = "aviso";
    public const string CouponKey = "cupao";
    public const string CouponUnknown = "Unknown";
    public const string CouponApplied = "Applied";
    public const string CouponRemoved = "Removed";

    private const string Prefix = "/carrinho";

    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Prefix).RequireRateLimiting(RateLimitPolicies.Checkout);
        group.MapPost("/adicionar", (Delegate)AddAsync);
        group.MapPost("/comprar-agora", (Delegate)BuyNowAsync);
        group.MapPost("/atualizar", (Delegate)UpdateAsync);
        group.MapPost("/local", (Delegate)LocationAsync);
        group.MapPost("/remover", (Delegate)RemoveAsync);
        group.MapPost("/cupao", (Delegate)CouponAsync);
        return endpoints;
    }

    public static bool IsLocalPath(string? value) =>
        !string.IsNullOrEmpty(value)
        && value[0] == '/'
        && (value.Length == 1 || (value[1] != '/' && value[1] != '\\'));

    private static async Task<IResult> AddAsync([FromForm] AddForm form, HttpContext context, ICartService carts)
    {
        var result = await AddToCartAsync(form, context, carts);
        var destination = IsLocalPath(form.ReturnUrl) ? form.ReturnUrl! : StoreUrls.Cart;
        return result.Change is CartChange.Added
            ? Results.LocalRedirect(destination)
            : Results.LocalRedirect(QueryHelpers.AddQueryString(StoreUrls.Cart, NoticeKey, result.Change.ToString()));
    }

    private static async Task<IResult> BuyNowAsync([FromForm] AddForm form, HttpContext context, ICartService carts)
    {
        var result = await AddToCartAsync(form, context, carts);
        return result.Change is CartChange.Added or CartChange.LimitedByStock
            ? Results.LocalRedirect(StoreUrls.Checkout)
            : Results.LocalRedirect(QueryHelpers.AddQueryString(StoreUrls.Cart, NoticeKey, result.Change.ToString()));
    }

    private static async Task<IResult> UpdateAsync([FromForm] UpdateForm form, HttpContext context, ICartService carts)
    {
        var cartId = CartCookies.Read(context.Request);
        if (cartId is not null)
        {
            await carts.SetQuantityAsync(cartId.Value, form.ProductId, form.Quantity, context.RequestAborted);
        }

        return Results.LocalRedirect(StoreUrls.Cart);
    }

    private static async Task<IResult> LocationAsync([FromForm] LocationForm form, HttpContext context, ICartService carts)
    {
        var cartId = CartCookies.Read(context.Request);
        if (cartId is null)
        {
            return Results.LocalRedirect(StoreUrls.Cart);
        }

        var change = await carts.SetLocationAsync(cartId.Value, form.ProductId, form.LocationId, context.RequestAborted);
        return change is CartChange.LocationChanged
            ? Results.LocalRedirect(StoreUrls.Cart)
            : Results.LocalRedirect(QueryHelpers.AddQueryString(StoreUrls.Cart, NoticeKey, change.ToString()));
    }

    private static async Task<IResult> RemoveAsync([FromForm] RemoveForm form, HttpContext context, ICartService carts)
    {
        var cartId = CartCookies.Read(context.Request);
        if (cartId is not null)
        {
            await carts.RemoveAsync(cartId.Value, form.ProductId, context.RequestAborted);
        }

        return Results.LocalRedirect(StoreUrls.Cart);
    }

    private static async Task<IResult> CouponAsync([FromForm] CouponForm form, HttpContext context, ICartService carts)
    {
        var cartId = CartCookies.Read(context.Request);
        if (cartId is null)
        {
            return Results.LocalRedirect(StoreUrls.Cart);
        }

        if (string.IsNullOrEmpty(form.Code))
        {
            await carts.RemoveCouponAsync(cartId.Value, context.RequestAborted);
            return Results.LocalRedirect(QueryHelpers.AddQueryString(StoreUrls.Cart, CouponKey, CouponRemoved));
        }

        var result = await carts.ApplyCouponAsync(cartId.Value, form.Code, context.RequestAborted);
        var feedback = result.Outcome switch
        {
            CouponApplyOutcome.Applied => CouponApplied,
            CouponApplyOutcome.Rejected => result.Rejection.ToString()!,
            _ => CouponUnknown,
        };
        return Results.LocalRedirect(QueryHelpers.AddQueryString(StoreUrls.Cart, CouponKey, feedback));
    }

    private static async Task<CartChangeResult> AddToCartAsync(AddForm form, HttpContext context, ICartService carts)
    {
        var result = await carts.AddAsync(
            CartCookies.Read(context.Request),
            CartCookies.ReadUserId(context.User),
            form.ProductId,
            form.Quantity < 1 ? 1 : form.Quantity,
            context.RequestAborted);
        if (result.CartId != Guid.Empty)
        {
            CartCookies.Write(context, result.CartId);
        }

        return result;
    }

    public sealed class AddForm
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public string? ReturnUrl { get; set; }
    }

    public sealed class UpdateForm
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class LocationForm
    {
        public int ProductId { get; set; }
        public int LocationId { get; set; }
    }

    public sealed class RemoveForm
    {
        public int ProductId { get; set; }
    }

    public sealed class CouponForm
    {
        public string? Code { get; set; }
    }
}

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

public static class CheckoutEndpoints
{
    public const string ErrorKey = "erro";
    public const string DetailKey = "detalhe";
    public const string NameKey = "nome";
    public const string PhoneKey = "telefone";
    public const string EmailKey = "email";
    public const string NotesKey = "notas";
    public const string MethodKey = "metodo";

    private const string ConfirmPath = "/checkout/confirmar";
    private const string CheckoutPath = "/checkout";
    private const string PaySuiteWebhookPath = "/pagamentos/paysuite/webhook";
    private const string SignatureHeader = "X-Signature";

    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCartEndpoints();
        endpoints.MapPost(ConfirmPath, (Delegate)ConfirmAsync).RequireRateLimiting(RateLimitPolicies.Checkout);
        endpoints.MapPost(PaySuiteWebhookPath, (Delegate)PaySuiteWebhookAsync).DisableAntiforgery().RequireRateLimiting(RateLimitPolicies.Webhook);
        return endpoints;
    }

    public static string OrderUrl(string number) => $"/pedido/{number}";

    private static async Task<IResult> ConfirmAsync(
        [FromForm] CheckoutForm form, HttpContext context, IOrderPlacement placement, IDataProtectionProvider protection)
    {
        var request = new PlaceOrderRequest(
            CartCookies.Read(context.Request),
            CartCookies.ReadUserId(context.User),
            form.Name,
            form.Phone,
            form.Email,
            form.Notes,
            Enum.TryParse<PaymentMethod>(form.Method, out var method) ? method : PaymentMethod.MPesa);
        var result = await placement.PlaceAsync(request, context.RequestAborted);
        if (!result.Succeeded)
        {
            return Results.LocalRedirect(FailureUrl(form, result));
        }

        PlacedOrdersCookie.Append(context, protection, result.OrderNumber!);
        return result.CheckoutUrl is { } checkoutUrl && SafeUrl.ToUrl(checkoutUrl) == checkoutUrl
            ? Results.Redirect(checkoutUrl)
            : Results.LocalRedirect(OrderUrl(result.OrderNumber!));
    }

    private static async Task<IResult> PaySuiteWebhookAsync(HttpContext context, IPaymentService payments)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(context.RequestAborted);
        var outcome = await payments.HandlePaySuiteWebhookAsync(body, context.Request.Headers[SignatureHeader], context.RequestAborted);
        return outcome == WebhookOutcome.InvalidSignature ? Results.Unauthorized() : Results.Ok();
    }

    private static string FailureUrl(CheckoutForm form, PlaceOrderResult result)
    {
        var query = new Dictionary<string, string?>
        {
            [ErrorKey] = result.Outcome.ToString(),
            [DetailKey] = result.Detail,
            [NameKey] = form.Name,
            [PhoneKey] = form.Phone,
            [EmailKey] = form.Email,
            [NotesKey] = form.Notes,
            [MethodKey] = form.Method,
        };
        return QueryHelpers.AddQueryString(CheckoutPath, query.Where(pair => !string.IsNullOrEmpty(pair.Value)));
    }

    public sealed class CheckoutForm
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public string? Method { get; set; }
    }
}

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

public static class HealthEndpoint
{
    private const string Route = "/health";
    private const string OkStatus = "ok";
    private const string PostgresName = "postgres";
    private const string SqliteName = "sqlite";
    private const string S3Name = "s3";
    private const string LocalName = "local";

    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, () => Results.Ok(new
        {
            status = OkStatus,
            database = AppEnvConfig.UsePostgres ? PostgresName : SqliteName,
            storage = AppEnvConfig.UseS3 ? S3Name : LocalName,
        }));
        return endpoints;
    }
}
