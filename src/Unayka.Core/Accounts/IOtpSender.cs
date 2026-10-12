namespace Unayka.Core.Accounts;

public interface IOtpSender
{
    /// <summary>Delivers a login code to the phone through the configured provider.</summary>
    Task SendAsync(string phone, string code, CancellationToken cancellationToken = default);
}

public interface IDevelopmentOtpInbox
{
    /// <summary>Remembers the last code issued for a phone so the development login page can show it.</summary>
    void Store(string phone, string code);

    /// <summary>Returns the last code issued for a phone, or null when none exists.</summary>
    string? Peek(string phone);
}
