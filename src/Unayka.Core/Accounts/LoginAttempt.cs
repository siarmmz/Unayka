namespace Unayka.Core.Accounts;

/// <summary>Record of a sign in attempt. The address is stored only as a hash.</summary>
public sealed class LoginAttempt
{
    public int Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public DateTimeOffset At { get; set; }
    public string IpHash { get; set; } = string.Empty;
}
