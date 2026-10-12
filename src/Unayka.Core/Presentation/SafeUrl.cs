namespace Unayka.Core.Presentation;

public static class SafeUrl
{
    public const string FallbackUrl = "/";
    public const string SelfTarget = "_self";
    public const string BlankTarget = "_blank";

    private const string MailScheme = "mailto:";
    private const string TelScheme = "tel:";

    public static string ToUrl(string? value)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate) || ContainsControlCharacter(candidate))
        {
            return FallbackUrl;
        }

        return IsLocalPath(candidate) || IsContactScheme(candidate) || IsWebUrl(candidate)
            ? candidate
            : FallbackUrl;
    }

    public static string ToTarget(string? value, string? publicBaseUrl = null)
    {
        var url = ToUrl(value);
        if (!IsWebUrl(url))
        {
            return SelfTarget;
        }

        return IsSameHost(url, publicBaseUrl) ? SelfTarget : BlankTarget;
    }

    private static bool IsLocalPath(string value) =>
        (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) && !value.StartsWith("/\\", StringComparison.Ordinal))
        || value.StartsWith('#');

    private static bool IsContactScheme(string value) =>
        value.StartsWith(MailScheme, StringComparison.OrdinalIgnoreCase)
        || value.StartsWith(TelScheme, StringComparison.OrdinalIgnoreCase);

    private static bool IsWebUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsSameHost(string url, string? publicBaseUrl) =>
        !string.IsNullOrEmpty(publicBaseUrl)
        && Uri.TryCreate(url, UriKind.Absolute, out var target)
        && Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri)
        && string.Equals(target.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsControlCharacter(string value) =>
        value.Any(char.IsControl);
}
