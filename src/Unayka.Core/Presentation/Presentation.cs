using System.Globalization;
using System.Net;
using System.Text;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Presentation;

public static class Formatters
{
    private const int MinorUnitsPerUnit = 100;
    private const string CurrencyCode = "MZN";
    private const string CountryCode = "258";
    private const int LocalNumberLength = 9;
    private const string NonBreakingSpace = "\u00A0";
    private static readonly TimeSpan MaputoOffset = TimeSpan.FromHours(2);

    public static string FormatMoney(long minorUnits)
    {
        var amount = minorUnits / (decimal)MinorUnitsPerUnit;
        var text = amount.ToString("#,##0.00", CultureInfo.InvariantCulture)
            .Replace(",", NonBreakingSpace, StringComparison.Ordinal)
            .Replace(".", ",", StringComparison.Ordinal);
        return $"{text}{NonBreakingSpace}{CurrencyCode}";
    }

    public static string FormatDate(DateTimeOffset value) =>
        value.ToOffset(MaputoOffset).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string FormatPhone(string? value)
    {
        var digits = new StringBuilder();
        foreach (var character in value ?? string.Empty)
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
        }

        var number = digits.ToString();
        if (number.StartsWith(CountryCode, StringComparison.Ordinal) && number.Length == CountryCode.Length + LocalNumberLength)
        {
            number = number[CountryCode.Length..];
        }

        if (number.Length != LocalNumberLength)
        {
            return value ?? string.Empty;
        }

        return $"+{CountryCode} {number[..2]} {number[2..5]} {number[5..]}";
    }
}

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

public enum StatusTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger,
}

public static class StatusLabels
{
    public static (string Label, StatusTone Tone) For(OrderStatus status) => status switch
    {
        OrderStatus.New => ("Novo", StatusTone.Info),
        OrderStatus.AwaitingPayment => ("A aguardar pagamento", StatusTone.Warning),
        OrderStatus.Paid => ("Pago", StatusTone.Success),
        OrderStatus.Preparing => ("Em preparação", StatusTone.Info),
        OrderStatus.Ready => ("Pronto", StatusTone.Success),
        OrderStatus.Delivered => ("Entregue", StatusTone.Neutral),
        OrderStatus.Cancelled => ("Cancelado", StatusTone.Danger),
        _ => (status.ToString(), StatusTone.Neutral),
    };

    public static (string Label, StatusTone Tone) For(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => ("Pendente", StatusTone.Warning),
        PaymentStatus.Paid => ("Pago", StatusTone.Success),
        PaymentStatus.Failed => ("Falhou", StatusTone.Danger),
        PaymentStatus.Cancelled => ("Cancelado", StatusTone.Neutral),
        _ => (status.ToString(), StatusTone.Neutral),
    };

    public static string For(PaymentMethod method) => method switch
    {
        PaymentMethod.MPesa => "M-Pesa",
        PaymentMethod.EMola => "e-Mola",
        PaymentMethod.MKesh => "mKesh",
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.Card => "Cartão",
        _ => method.ToString(),
    };
}
