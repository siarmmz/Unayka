using System.Net;

namespace Unayka.Core.Presentation;

public static class SafeText
{
    private static readonly (string Encoded, string Html)[] AllowedInlineTags =
    [
        ("&lt;b&gt;", "<b>"),
        ("&lt;/b&gt;", "</b>"),
        ("&lt;i&gt;", "<i>"),
        ("&lt;/i&gt;", "</i>"),
        ("&lt;br&gt;", "<br>"),
        ("&lt;br/&gt;", "<br>"),
    ];

    public static string ToHtml(string? value, bool format = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var encoded = WebUtility.HtmlEncode(value);
        if (!format)
        {
            return encoded;
        }

        foreach (var (tagEncoded, tagHtml) in AllowedInlineTags)
        {
            encoded = encoded.Replace(tagEncoded, tagHtml, StringComparison.OrdinalIgnoreCase);
        }

        return encoded;
    }
}
