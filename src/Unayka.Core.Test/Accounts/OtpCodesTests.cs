using Unayka.Core.Accounts;

namespace Unayka.Core.Test.Accounts;

public sealed class OtpCodesTests
{
    private const string Phone = "841234567";

    [Fact]
    public void Generated_code_has_six_unambiguous_uppercase_characters()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var code = OtpCodes.Generate();

            Assert.Equal(OtpCodes.Length, code.Length);
            Assert.All(code, character => Assert.DoesNotContain(character, "01OI"));
            Assert.Equal(code.ToUpperInvariant(), code);
        }
    }

    [Fact]
    public void Hash_does_not_contain_the_code_and_depends_on_the_phone()
    {
        var hash = OtpCodes.Hash(Phone, "ABC234");

        Assert.DoesNotContain("ABC234", hash, StringComparison.Ordinal);
        Assert.NotEqual(hash, OtpCodes.Hash("842345678", "ABC234"));
    }

    [Fact]
    public void Matches_ignores_case_and_surrounding_spaces()
    {
        var hash = OtpCodes.Hash(Phone, "ABC234");

        Assert.True(OtpCodes.Matches(Phone, " abc234 ", hash));
        Assert.False(OtpCodes.Matches(Phone, "ABC235", hash));
    }

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
