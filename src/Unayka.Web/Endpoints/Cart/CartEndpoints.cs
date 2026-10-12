using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Web.Endpoints.Security;

namespace Unayka.Web.Endpoints.Cart;

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

    public sealed class RemoveForm
    {
        public int ProductId { get; set; }
    }

    public sealed class CouponForm
    {
        public string? Code { get; set; }
    }
}
