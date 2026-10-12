using Microsoft.AspNetCore.Components;

namespace Unayka.Web.Components.Base;

public abstract class UnaykaLayoutBase : LayoutComponentBase
{
    protected static MarkupString Text(string value, bool format = false) =>
        UnaykaView.Text(value, format);

    protected static string ToUrl(string? value) => UnaykaView.ToUrl(value);

    protected static string ToUrlTarget(string? value) => UnaykaView.ToUrlTarget(value);

    protected static string FormatMoney(long minorUnits) => UnaykaView.FormatMoney(minorUnits);

    protected static string FormatDate(DateTimeOffset value) => UnaykaView.FormatDate(value);

    protected static string FormatPhone(string? value) => UnaykaView.FormatPhone(value);
}
