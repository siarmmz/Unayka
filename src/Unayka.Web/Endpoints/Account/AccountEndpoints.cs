using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Unayka.Core.Accounts;
using Unayka.Core.Catalog;
using Unayka.Web.Endpoints.Security;

namespace Unayka.Web.Endpoints.Account;

public static class AccountEndpoints
{
    public const string VerifyStep = "verify";
    public const string StepKey = "step";
    public const string PhoneKey = "phone";
    public const string ErrorKey = "error";
    public const string ReturnUrlKey = "returnUrl";

    private const string Prefix = "/conta";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Prefix);
        group.MapPost("/entrar/codigo", (Delegate)RequestCodeAsync).RequireRateLimiting(RateLimitPolicies.Code);
        group.MapPost("/entrar/verificar", (Delegate)VerifyCodeAsync).RequireRateLimiting(RateLimitPolicies.Login);
        group.MapPost("/entrar/equipa", (Delegate)SignInStaffAsync).RequireRateLimiting(RateLimitPolicies.Login);
        group.MapPost("/sair", (Delegate)SignOutAsync);
        return endpoints;
    }

    private static async Task<IResult> RequestCodeAsync(
        [FromForm] PhoneForm form, HttpContext context, IAccountService accounts)
    {
        var outcome = await accounts.RequestCodeAsync(form.Phone, ClientHash(context), context.RequestAborted);
        return outcome == AccountOutcome.Succeeded
            ? Results.Redirect(LoginUrl(form.ReturnUrl, (StepKey, VerifyStep), (PhoneKey, PhoneNumber.Normalize(form.Phone))))
            : Results.Redirect(LoginUrl(form.ReturnUrl, (ErrorKey, outcome.ToString())));
    }

    private static async Task<IResult> VerifyCodeAsync(
        [FromForm] VerifyForm form, HttpContext context, IAccountService accounts)
    {
        var result = await accounts.VerifyCodeAsync(
            form.Phone, form.Code, form.Name, ClientHash(context), context.RequestAborted);
        if (!result.Succeeded)
        {
            return Results.Redirect(LoginUrl(
                form.ReturnUrl, (StepKey, VerifyStep), (PhoneKey, PhoneNumber.Normalize(form.Phone)), (ErrorKey, result.Outcome.ToString())));
        }

        return await SignInAsync(context, result.User!, form.ReturnUrl);
    }

    private static async Task<IResult> SignInStaffAsync(
        [FromForm] StaffForm form, HttpContext context, IAccountService accounts)
    {
        var result = await accounts.SignInWithPasswordAsync(
            form.Email, form.Password, ClientHash(context), context.RequestAborted);
        if (!result.Succeeded)
        {
            return Results.Redirect(LoginUrl(form.ReturnUrl, (ErrorKey, result.Outcome.ToString())));
        }

        return await SignInAsync(context, result.User!, form.ReturnUrl);
    }

    private static async Task<IResult> SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/");
    }

    private static async Task<IResult> SignInAsync(HttpContext context, User user, string? returnUrl)
    {
        var principal = CreatePrincipal(user);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });
        return Results.LocalRedirect(ResolveDestination(returnUrl, user));
    }

    private static ClaimsPrincipal CreatePrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        if (user.Email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (user.Phone is not null)
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.Phone));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static string ResolveDestination(string? returnUrl, User user)
    {
        if (IsLocalPath(returnUrl))
        {
            return returnUrl!;
        }

        return user.Role == UserRole.Customer ? StoreUrls.Account : StoreUrls.Admin;
    }

    private static bool IsLocalPath(string? value) =>
        !string.IsNullOrEmpty(value)
        && value[0] == '/'
        && (value.Length == 1 || (value[1] != '/' && value[1] != '\\'));

    private static string ClientHash(HttpContext context) =>
        IpHash.Of(context.Connection.RemoteIpAddress?.ToString());

    private static string LoginUrl(string? returnUrl, params (string Key, string? Value)[] values)
    {
        var query = values
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (IsLocalPath(returnUrl))
        {
            query[ReturnUrlKey] = returnUrl;
        }

        return QueryHelpers.AddQueryString(StoreUrls.SignIn, query);
    }

    public sealed class PhoneForm
    {
        public string? Phone { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public sealed class VerifyForm
    {
        public string? Phone { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public sealed class StaffForm
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
