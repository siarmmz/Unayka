using Unayka.Core.Presentation;

namespace Unayka.Core.Test.Presentation;

public sealed class SafeTextTests
{
    [Fact]
    public void ToHtml_escapes_markup()
    {
        Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;", SafeText.ToHtml("<script>alert(1)</script>"));
    }

    [Fact]
    public void ToHtml_without_format_escapes_inline_tags()
    {
        Assert.Equal("&lt;b&gt;x&lt;/b&gt;", SafeText.ToHtml("<b>x</b>"));
    }

    [Fact]
    public void ToHtml_with_format_allows_only_inline_tags()
    {
        Assert.Equal("<b>x</b><br>&lt;a href=&quot;/&quot;&gt;", SafeText.ToHtml("<b>x</b><br><a href=\"/\">", format: true));
    }

    [Fact]
    public void ToHtml_returns_empty_for_null()
    {
        Assert.Equal(string.Empty, SafeText.ToHtml(null));
    }
}
