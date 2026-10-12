using Unayka.Core.Presentation;

namespace Unayka.Core.Test.Presentation;

public sealed class FormattersTests
{
    [Fact]
    public void FormatMoney_uses_comma_decimals_and_currency()
    {
        Assert.Equal("1\u00A0250,50\u00A0MZN", Formatters.FormatMoney(125050));
    }

    [Fact]
    public void FormatMoney_formats_zero()
    {
        Assert.Equal("0,00\u00A0MZN", Formatters.FormatMoney(0));
    }

    [Theory]
    [InlineData("841234567")]
    [InlineData("+258 84 123 4567")]
    [InlineData("258841234567")]
    public void FormatPhone_normalizes_mozambican_numbers(string value)
    {
        Assert.Equal("+258 84 123 4567", Formatters.FormatPhone(value));
    }

    [Fact]
    public void FormatPhone_keeps_unknown_values()
    {
        Assert.Equal("12345", Formatters.FormatPhone("12345"));
    }

    [Fact]
    public void FormatDate_uses_maputo_time()
    {
        var value = new DateTimeOffset(2026, 10, 12, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal("13/10/2026", Formatters.FormatDate(value));
    }
}
