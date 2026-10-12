using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Unayka.Core.Accounts;
using Unayka.Core.Catalog;

namespace Unayka.Web.Endpoints;

public static class AccountEndpoints
{
    public const string ErrorKey = "error";
    public const string ReturnUrlKey = "returnUrl";

    private const string Prefix = "/conta";
    private const string ExternalPath = "/conta/entrar/externo/";
    private const string ExternalDonePath = "/conta/entrar/externo/concluir";
    public const string ExternalUnavailableError = "ExternalUnavailable";
    private const string LoginPath = "/conta/entrar";

    public const string SetupPath = "/configurar";
    private const string SetupErrorKey = "erro";
    private static readonly string[] OpenPrefixes = [SetupPath, "/_framework", "/_blazor", "/styles", "/scripts", "/images", "/health", "/favicon"];
    private static volatile bool _configured;

    /// <summary>Keeps every request on the setup screen until the first administrator exists.</summary>
    public static IApplicationBuilder UseUnaykaSetupGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (_configured || OpenPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) || Path.HasExtension(path))
            {
                await next();
                return;
            }

            _configured = await context.RequestServices.GetRequiredService<IAccountService>().IsConfiguredAsync(context.RequestAborted);
            if (_configured)
            {
                await next();
                return;
            }

            context.Response.Redirect(SetupPath);
        });

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(SetupPath, (Delegate)SetupAsync).RequireRateLimiting(RateLimitPolicies.Login);
        var group = endpoints.MapGroup(Prefix);
        group.MapGet("/entrar/externo/concluir", (Delegate)CompleteExternalAsync).RequireRateLimiting(RateLimitPolicies.Login);
        group.MapGet("/entrar/externo/{provider}", (Delegate)StartExternal).RequireRateLimiting(RateLimitPolicies.Login);
        group.MapPost("/entrar/equipa", (Delegate)SignInStaffAsync).RequireRateLimiting(RateLimitPolicies.Login);
        group.MapPost("/sair", (Delegate)SignOutAsync);
        return endpoints;
    }

    private static async Task<IResult> SetupAsync([FromForm] SetupForm form, IAccountService accounts, HttpContext context)
    {
        var outcome = await accounts.CreateFirstAdministratorAsync(
            new SetupRequest(form.Email, form.EmailConfirmation, form.Password, form.PasswordConfirmation), context.RequestAborted);
        return outcome switch
        {
            SetupOutcome.Created or SetupOutcome.AlreadyConfigured => Results.LocalRedirect(LoginPath),
            _ => Results.LocalRedirect(QueryHelpers.AddQueryString(SetupPath, SetupErrorKey, outcome.ToString())),
        };
    }

    public sealed class SetupForm
    {
        public string? Email { get; set; }
        public string? EmailConfirmation { get; set; }
        public string? Password { get; set; }
        public string? PasswordConfirmation { get; set; }
    }

    public static string ExternalUrl(string provider, string? returnUrl) =>
        IsLocalPath(returnUrl)
            ? QueryHelpers.AddQueryString(ExternalPath + provider, ReturnUrlKey, returnUrl!)
            : ExternalPath + provider;

    private static IResult StartExternal(string provider, string? returnUrl)
    {
        var known = ExternalProviders.All.FirstOrDefault(candidate => string.Equals(candidate, provider, StringComparison.OrdinalIgnoreCase));
        if (known is null)
        {
            return Results.NotFound();
        }

        if (!ExternalProviders.IsConfigured(known))
        {
            return Results.LocalRedirect(QueryHelpers.AddQueryString(LoginPath, ErrorKey, ExternalUnavailableError));
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
