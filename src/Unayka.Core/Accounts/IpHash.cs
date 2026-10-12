using System.Security.Cryptography;
using System.Text;

namespace Unayka.Core.Accounts;

public static class IpHash
{
    private const int HashLength = 16;

    public static string Of(string? address) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address ?? string.Empty)))[..HashLength];
}
