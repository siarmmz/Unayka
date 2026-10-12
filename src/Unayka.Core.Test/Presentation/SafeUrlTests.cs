using Unayka.Core.Presentation;

namespace Unayka.Core.Test.Presentation;

public sealed class SafeUrlTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.example.com")]
    [InlineData("/\evil.example.com")]
    [InlineData("java\nscript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void ToUrl_rejects_unsafe_values(string? value)
    {
        Assert.Equal(SafeUrl.FallbackUrl, SafeUrl.ToUrl(value));
    }

    [Theory]
    [InlineData("/contact")]
    [InlineData("#top")]
    [InlineData("mailto:ola@unayka.co.mz")]
    [InlineData("tel:+258841234567")]
    [InlineData("https://example.com/page")]
    public void ToUrl_keeps_safe_values(string value)
    {
        Assert.Equal(value, SafeUrl.ToUrl(value));
    }

    [Theory]
    [InlineData("/contact", SafeUrl.SelfTarget)]
    [InlineData("mailto:ola@unayka.co.mz", SafeUrl.SelfTarget)]
    [InlineData("tel:+258841234567", SafeUrl.SelfTarget)]
    [InlineData("https://example.com", SafeUrl.BlankTarget)]
    [InlineData("javascript:alert(1)", SafeUrl.SelfTarget)]
    public void ToTarget_matches_url_kind(string value, string expected)
    {
        Assert.Equal(expected, SafeUrl.ToTarget(value));
    }

    [Fact]
    public void ToTarget_treats_public_host_as_local()
    {
        Assert.Equal(SafeUrl.SelfTarget, SafeUrl.ToTarget("https://unayka.co.mz/about", "https://unayka.co.mz"));
    }
}
