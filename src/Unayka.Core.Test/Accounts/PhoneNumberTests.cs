using Unayka.Core.Accounts;

namespace Unayka.Core.Test.Accounts;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("841234567", "841234567")]
    [InlineData("+258 84 123 4567", "841234567")]
    [InlineData("258841234567", "841234567")]
    [InlineData("12345", null)]
    [InlineData("741234567", null)]
    [InlineData(null, null)]
    public void Phone_is_normalized_to_the_national_number(string? input, string? expected)
    {
        Assert.Equal(expected, PhoneNumber.Normalize(input));
    }
}
