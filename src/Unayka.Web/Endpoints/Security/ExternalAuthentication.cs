using Microsoft.AspNetCore.Authentication;
using Unayka.Core.Accounts;
using Unayka.Core.Configuration;

namespace Unayka.Web.Endpoints.Security;

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
