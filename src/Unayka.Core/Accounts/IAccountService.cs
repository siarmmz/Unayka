namespace Unayka.Core.Accounts;

public enum AccountOutcome
{
    Succeeded,
    InvalidIdentity,
    InvalidCredentials,
    LockedOut,
}

public sealed record AccountResult(AccountOutcome Outcome, User? User = null)
{
    public bool Succeeded => Outcome == AccountOutcome.Succeeded;
}

/// <summary>Identity confirmed by an external provider such as Google or Facebook.</summary>
public sealed record ExternalIdentity(string Provider, string Key, string Name, string? Email);

public interface IAccountService
{
    /// <summary>Returns the customer linked to the external identity, creating or linking the account when needed.</summary>
    Task<AccountResult> SignInExternalAsync(ExternalIdentity identity, string ipHash, CancellationToken cancellationToken = default);

    /// <summary>Checks staff email and password.</summary>
    Task<AccountResult> SignInWithPasswordAsync(
        string? email, string? password, string ipHash, CancellationToken cancellationToken = default);
}
