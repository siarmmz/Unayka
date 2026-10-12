using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Configuration;
using Unayka.Core.Data;

namespace Unayka.Core.Accounts;

public sealed class AccountService(
    UnaykaDbContext database,
    IPasswordHasher<User> passwordHasher,
    TimeProvider time) : IAccountService
{
    private const int MaxNameLength = 80;
    private const string DefaultCustomerName = "Cliente";
    private const int MaxFailuresBeforeLockout = 10;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    public async Task<AccountResult> SignInExternalAsync(
        ExternalIdentity identity, string ipHash, CancellationToken cancellationToken = default)
    {
        if (!ExternalProviders.IsKnown(identity.Provider) || string.IsNullOrWhiteSpace(identity.Key))
        {
            return new AccountResult(AccountOutcome.InvalidIdentity);
        }

        var now = time.GetUtcNow();
        var email = NormalizeEmail(identity.Email);
        var user = await database.Users.FirstOrDefaultAsync(
                       candidate => candidate.ExternalProvider == identity.Provider && candidate.ExternalKey == identity.Key, cancellationToken)
                   ?? await FindCustomerByEmailAsync(email, cancellationToken);
        user ??= CreateCustomer(identity.Name, email, now);

        user.ExternalProvider ??= identity.Provider;
        user.ExternalKey ??= identity.Key;
        user.LastLoginAt = now;
        RecordAttempt($"{identity.Provider}:{identity.Key}", true, ipHash, now);
        await database.SaveChangesAsync(cancellationToken);
        return new AccountResult(AccountOutcome.Succeeded, user);
    }

    public async Task<AccountResult> SignInWithPasswordAsync(
        string? email, string? password, string ipHash, CancellationToken cancellationToken = default)
    {
        var identifier = (email ?? string.Empty).Trim().ToLowerInvariant();
        var now = time.GetUtcNow();
        if (await IsLockedOutAsync(identifier, now, cancellationToken))
        {
            return new AccountResult(AccountOutcome.LockedOut);
        }

        var user = identifier.Length == 0
            ? null
            : await database.Users.FirstOrDefaultAsync(candidate => candidate.Email == identifier, cancellationToken);
        if (user?.PasswordHash is null || !PasswordMatches(user, password ?? string.Empty))
        {
            return await FailAsync(identifier, ipHash, AccountOutcome.InvalidCredentials, cancellationToken);
        }

        PromoteBootstrapAdministrator(user);
        user.LastLoginAt = now;
        RecordAttempt(identifier, true, ipHash, now);
        await database.SaveChangesAsync(cancellationToken);
        return new AccountResult(AccountOutcome.Succeeded, user);
    }

    private static void PromoteBootstrapAdministrator(User user)
    {
        var bootstrapEmail = AppEnvConfig.AdminBootstrapEmail.ToLowerInvariant();
        if (bootstrapEmail.Length > 0 && user.Email == bootstrapEmail)
        {
            user.Role = UserRole.Administrator;
        }
    }

    private bool PasswordMatches(User user, string password)
    {
        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, password);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        return result != PasswordVerificationResult.Failed;
    }

    private User CreateCustomer(string name, string? email, DateTimeOffset now)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? DefaultCustomerName : name.Trim();
        var user = new User
        {
            Name = trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed,
            Email = email,
            Role = UserRole.Customer,
            CreatedAt = now,
        };
        database.Users.Add(user);
        return user;
    }

    private async Task<User?> FindCustomerByEmailAsync(string? email, CancellationToken cancellationToken) =>
        email is null
            ? null
            : await database.Users.FirstOrDefaultAsync(candidate => candidate.Email == email && candidate.Role == UserRole.Customer, cancellationToken);

    private static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private async Task<bool> IsLockedOutAsync(string identifier, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now - LockoutWindow;
        var failures = await database.LoginAttempts.CountAsync(
            attempt => attempt.Identifier == identifier && !attempt.Succeeded && attempt.At > since, cancellationToken);
        return failures >= MaxFailuresBeforeLockout;
    }

    private async Task<AccountResult> FailAsync(
        string identifier, string ipHash, AccountOutcome outcome, CancellationToken cancellationToken)
    {
        RecordAttempt(identifier, false, ipHash, time.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return new AccountResult(outcome);
    }

    private void RecordAttempt(string identifier, bool succeeded, string ipHash, DateTimeOffset now)
    {
        database.LoginAttempts.Add(new LoginAttempt
        {
            Identifier = identifier,
            Succeeded = succeeded,
            At = now,
            IpHash = ipHash,
        });
    }
}
