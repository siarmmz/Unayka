using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Unayka.Core.Accounts;
using Unayka.Core.Catalog;
using Unayka.Core.Configuration;

namespace Unayka.Web.Endpoints;

public static class SecurityExtensions
{
    private const string AuthCookieName = "unayka_auth";
    private const string AntiforgeryCookieName = "unayka_af";
    private const string UnknownClient = "unknown";
    private static readonly TimeSpan SessionLength = TimeSpan.FromDays(14);
    private static readonly TimeSpan MinuteWindow = TimeSpan.FromMinutes(1);

    private const string IconsCdn = "https://cdn.jsdelivr.net";

    private static readonly string ContentSecurityPolicy = string.Join("; ",
        "default-src 'self'",
        "script-src 'self' 'unsafe-inline'",
        $"style-src 'self' 'unsafe-inline' https://fonts.googleapis.com {IconsCdn}",
        $"font-src 'self' https://fonts.gstatic.com {IconsCdn}",
        "img-src 'self' data: https:",
        "connect-src 'self' ws: wss:",
        "frame-src https://www.openstreetmap.org",
        "object-src 'none'",
        "base-uri 'self'",
        $"form-action 'self' {PaymentOrigin()}".TrimEnd(),
        "frame-ancestors 'none'");

    public static IServiceCollection AddUnaykaSecurity(this IServiceCollection services, bool isDevelopment)
    {
        var securePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        services.AddCascadingAuthenticationState();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = AuthCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = securePolicy;
                options.ExpireTimeSpan = SessionLength;
                options.SlidingExpiration = true;
                options.LoginPath = StoreUrls.SignIn;
                options.AccessDeniedPath = StoreUrls.SignIn;
            })
            .AddUnaykaExternalProviders(securePolicy);

        services.AddAuthorizationBuilder()
            .AddPolicy(UnaykaPolicies.Staff, policy => policy.RequireRole(
                nameof(UserRole.Cashier), nameof(UserRole.Manager), nameof(UserRole.Administrator)))
            .AddPolicy(UnaykaPolicies.Management, policy => policy.RequireRole(
                nameof(UserRole.Manager), nameof(UserRole.Administrator)))
            .AddPolicy(UnaykaPolicies.Administration, policy => policy.RequireRole(nameof(UserRole.Administrator)));

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = securePolicy;
        });

        services.AddDataProtection()
            .SetApplicationName("Unayka")
            .PersistKeysToFileSystem(new DirectoryInfo(AppEnvConfig.DataProtectionKeysPath));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            AddFixedWindowPolicy(options, RateLimitPolicies.Login, () => AppEnvConfig.RateLimitLoginPerMinute, MinuteWindow);
            AddFixedWindowPolicy(options, RateLimitPolicies.Checkout, () => AppEnvConfig.RateLimitCheckoutPerMinute, MinuteWindow);
            AddFixedWindowPolicy(options, RateLimitPolicies.Webhook, () => AppEnvConfig.RateLimitWebhookPerMinute, MinuteWindow);
        });

        return services;
    }

    public static IApplicationBuilder UseUnaykaSecurity(this WebApplication app, bool isDevelopment)
    {
        if (!isDevelopment)
        {
            app.UseHsts();
        }

        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            await next();
        });

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    private static string PaymentOrigin() =>
        Uri.TryCreate(AppEnvConfig.PaySuiteBaseUrl, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : string.Empty;

    private static void AddFixedWindowPolicy(RateLimiterOptions options, string name, Func<int> limit, TimeSpan window) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit(),
                Window = window,
                QueueLimit = 0,
            }));
}

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Checkout = "checkout";
    public const string Webhook = "webhook";
}

public static class ExternalAuthentication
{
    public const string Scheme = "External";
    public const string ProviderItem = "provider";

    private const string CookieName = "unayka_external";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromMinutes(10);

    public static AuthenticationBuilder AddUnaykaExternalProviders(this AuthenticationBuilder builder, CookieSecurePolicy securePolicy)
    {
        builder.AddCookie(Scheme, options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = securePolicy;
            options.ExpireTimeSpan = CookieLifetime;
        });

        if (ExternalProviders.IsConfigured(ExternalProviders.Google))
        {
            builder.AddGoogle(ExternalProviders.Google, options =>
            {
                options.SignInScheme = Scheme;
                options.ClientId = AppEnvConfig.GoogleClientId;
                options.ClientSecret = AppEnvConfig.GoogleClientSecret;
            });
        }

        if (ExternalProviders.IsConfigured(ExternalProviders.Facebook))
        {
            builder.AddFacebook(ExternalProviders.Facebook, options =>
            {
                options.SignInScheme = Scheme;
                options.AppId = AppEnvConfig.FacebookAppId;
                options.AppSecret = AppEnvConfig.FacebookAppSecret;
            });
        }

        return builder;
    }
}
