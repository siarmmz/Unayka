using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Unayka.Core.Accounts;
using Unayka.Core.Catalog;
using Unayka.Core.Configuration;

namespace Unayka.Web.Endpoints.Security;

public static class SecurityExtensions
{
    private const string AuthCookieName = "unayka_auth";
    private const string AntiforgeryCookieName = "unayka_af";
    private const string UnknownClient = "unknown";
    private static readonly TimeSpan SessionLength = TimeSpan.FromDays(14);
    private static readonly TimeSpan MinuteWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HourWindow = TimeSpan.FromHours(1);

    private static readonly string ContentSecurityPolicy = string.Join("; ",
        "default-src 'self'",
        "script-src 'self' 'unsafe-inline'",
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
        "font-src 'self' https://fonts.gstatic.com",
        "img-src 'self' data: https://images.unsplash.com",
        "connect-src 'self' ws: wss:",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
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
            });

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
            AddFixedWindowPolicy(options, RateLimitPolicies.Code, () => AppEnvConfig.RateLimitOtpPerHour, HourWindow);
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
