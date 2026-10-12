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
    public const string ErrorKey = "error";
    public const string ReturnUrlKey = "returnUrl";

    private const string Prefix = "/conta";
    private const string ExternalPath = "/conta/entrar/externo/";
    private const string ExternalDonePath = "/conta/entrar/externo/concluir";
    private const string MockKeyPrefix = "local-";
    private const string MockName = "Cliente de teste";
    private const string MockEmailDomain = "@exemplo.local";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Prefix);
        group.MapGet("/entrar/externo/concluir", (Delegate)CompleteExternalAsync).RequireRateLimiting(RateLimitPolicies.Login);
        group.MapGet("/entrar/externo/{provider}", (Delegate)StartExternalAsync).RequireRateLimiting(RateLimitPolicies.Login);
        group.MapPost("/entrar/equipa", (Delegate)SignInStaffAsync).RequireRateLimiting(RateLimitPolicies.Login);
        group.MapPost("/sair", (Delegate)SignOutAsync);
        return endpoints;
    }

    public static string ExternalUrl(string provider, string? returnUrl) =>
        IsLocalPath(returnUrl)
            ? QueryHelpers.AddQueryString(ExternalPath + provider, ReturnUrlKey, returnUrl!)
            : ExternalPath + provider;

    private static async Task<IResult> StartExternalAsync(
        string provider, string? returnUrl, HttpContext context, IAccountService accounts)
    {
        var known = ExternalProviders.All.FirstOrDefault(candidate => string.Equals(candidate, provider, StringComparison.OrdinalIgnoreCase));
        if (known is null || !ExternalProviders.IsAvailable(known))
        {
            return Results.NotFound();
        }

        if (ExternalProviders.IsMocked(known))
        {
            var mock = new ExternalIdentity(known, MockKeyPrefix + known.ToLowerInvariant(), $"{MockName} {known}", known.ToLowerInvariant() + MockEmailDomain);
            return await CompleteAsync(context, accounts, mock, returnUrl);
        }

        var done = IsLocalPath(returnUrl) ? QueryHelpers.AddQueryString(ExternalDonePath, ReturnUrlKey, returnUrl!) : ExternalDonePath;
        var properties = new AuthenticationProperties { RedirectUri = done };
        properties.Items[ExternalAuthentication.ProviderItem] = known;
        return Results.Challenge(properties, [known]);
    }

    private static async Task<IResult> CompleteExternalAsync(string? returnUrl, HttpContext context, IAccountService accounts)
    {
        var external = await context.AuthenticateAsync(ExternalAuthentication.Scheme);
        await context.SignOutAsync(ExternalAuthentication.Scheme);
        var principal = external.Principal;
        string? provider = null;
        external.Properties?.Items.TryGetValue(ExternalAuthentication.ProviderItem, out provider);
        var key = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!external.Succeeded || !ExternalProviders.IsKnown(provider) || key is null)
        {
            return Results.Redirect(LoginUrl(returnUrl, (ErrorKey, nameof(AccountOutcome.InvalidIdentity))));
        }

        var identity = new ExternalIdentity(provider!, key, principal!.FindFirstValue(ClaimTypes.Name) ?? string.Empty, principal!.FindFirstValue(ClaimTypes.Email));
        return await CompleteAsync(context, accounts, identity, returnUrl);
    }

    private static async Task<IResult> CompleteAsync(HttpContext context, IAccountService accounts, ExternalIdentity identity, string? returnUrl)
    {
        var result = await accounts.SignInExternalAsync(identity, ClientHash(context), context.RequestAborted);
        return result.Succeeded
            ? await SignInAsync(context, result.User!, returnUrl)
            : Results.Redirect(LoginUrl(returnUrl, (ErrorKey, result.Outcome.ToString())));
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

    public sealed class StaffForm
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
