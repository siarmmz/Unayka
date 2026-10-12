namespace Unayka.Core.Accounts;

/// <summary>Login code sent to a phone. Only the hash of the code is stored.</summary>
public sealed class OtpCode
{
    public int Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
