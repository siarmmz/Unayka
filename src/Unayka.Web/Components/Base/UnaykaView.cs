using Microsoft.AspNetCore.Components;
using Unayka.Core.Configuration;
using Unayka.Core.Presentation;

namespace Unayka.Web.Components.Base;

public static class UnaykaView
{
    public static MarkupString Text(string value, bool format = false) =>
        new(SafeText.ToHtml(value, format));

    public static string ToUrl(string? value) => SafeUrl.ToUrl(value);

    public static string ToUrlTarget(string? value) =>
        SafeUrl.ToTarget(value, AppEnvConfig.PublicBaseUrl);

    public static string FormatMoney(long minorUnits) => Formatters.FormatMoney(minorUnits);

    public static string FormatDate(DateTimeOffset value) => Formatters.FormatDate(value);

    public static string FormatPhone(string? value) => Formatters.FormatPhone(value);
}
