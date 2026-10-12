namespace Unayka.Core.Accounts;

public enum AccountOutcome
{
    Succeeded,
    InvalidPhone,
    InvalidCode,
    CodeExpired,
    TooManyAttempts,
    TooManyRequests,
    NameRequired,
    InvalidCredentials,
    LockedOut,
}

public sealed record AccountResult(AccountOutcome Outcome, User? User = null)
{
    public bool Succeeded => Outcome == AccountOutcome.Succeeded;
}

public interface IAccountService
{
    /// <summary>Creates and sends a login code to the phone.</summary>
    Task<AccountOutcome> RequestCodeAsync(string? phone, string ipHash, CancellationToken cancellationToken = default);

    /// <summary>Checks the code and returns the customer, creating the account when a name is supplied for a new phone.</summary>
    Task<AccountResult> VerifyCodeAsync(
        string? phone, string? code, string? name, string ipHash, CancellationToken cancellationToken = default);

    /// <summary>Checks staff email and password.</summary>
    Task<AccountResult> SignInWithPasswordAsync(
        string? email, string? password, string ipHash, CancellationToken cancellationToken = default);
}
