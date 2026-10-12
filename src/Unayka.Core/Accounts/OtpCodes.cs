using System.Security.Cryptography;
using System.Text;

namespace Unayka.Core.Accounts;

public static class OtpCodes
{
    public const int Length = 6;
    public const int MaxAttempts = 5;
    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(10);

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static string Hash(string phone, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{phone}:{Normalize(code)}")));

    public static bool Matches(string phone, string code, string expectedHash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(phone, code)),
            Encoding.UTF8.GetBytes(expectedHash));
}
