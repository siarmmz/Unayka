using Unayka.Core.Configuration;

namespace Unayka.Core.Accounts;

public static class ExternalProviders
{
    public const string Google = "Google";
    public const string Facebook = "Facebook";

    public static readonly IReadOnlyList<string> All = [Google, Facebook];

    public static bool IsKnown(string? provider) => provider is not null && All.Contains(provider);

    public static bool IsConfigured(string provider) => provider switch
    {
        Google => AppEnvConfig.GoogleClientId.Length > 0 && AppEnvConfig.GoogleClientSecret.Length > 0,
        Facebook => AppEnvConfig.FacebookAppId.Length > 0 && AppEnvConfig.FacebookAppSecret.Length > 0,
        _ => false,
    };

    public static bool IsMocked(string provider) => !AppEnvConfig.IsProduction && !IsConfigured(provider);

    public static bool IsAvailable(string provider) => IsConfigured(provider) || IsMocked(provider);
}
