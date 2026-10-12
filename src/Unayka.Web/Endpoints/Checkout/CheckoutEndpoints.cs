using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Web.Endpoints.Cart;
using Unayka.Web.Endpoints.Security;

namespace Unayka.Web.Endpoints.Checkout;

public static class CheckoutEndpoints
{
    public const string ErrorKey = "erro";
    public const string DetailKey = "detalhe";
    public const string NameKey = "nome";
    public const string PhoneKey = "telefone";
    public const string EmailKey = "email";
    public const string NotesKey = "notas";
    public const string MethodKey = "metodo";
    public const string WalletKey = "carteira";

    private const string ConfirmPath = "/checkout/confirmar";
    private const string CheckoutPath = "/checkout";

    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCartEndpoints();
        endpoints.MapPost(ConfirmPath, (Delegate)ConfirmAsync).RequireRateLimiting(RateLimitPolicies.Checkout);
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
            Enum.TryParse<PaymentMethod>(form.Method, out var method) ? method : PaymentMethod.MPesa,
            form.WalletPhone);
        var result = await placement.PlaceAsync(request, context.RequestAborted);
        if (!result.Succeeded)
        {
            return Results.LocalRedirect(FailureUrl(form, result));
        }

        PlacedOrdersCookie.Append(context, protection, result.OrderNumber!);
        return Results.LocalRedirect(OrderUrl(result.OrderNumber!));
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
            [WalletKey] = form.WalletPhone,
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
        public string? WalletPhone { get; set; }
    }
}
