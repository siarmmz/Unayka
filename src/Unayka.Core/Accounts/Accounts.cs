using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unayka.Core.Configuration;
using Unayka.Core.Data;

namespace Unayka.Core.Accounts;

/// <summary>Customer or staff member. Phone is stored as the nine digit national number.</summary>
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalKey { get; set; }
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public enum UserRole
{
    Customer,
    Cashier,
    Manager,
    Administrator,
}

/// <summary>Record of a sign in attempt. The address is stored only as a hash.</summary>
public sealed class LoginAttempt
{
    public int Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public DateTimeOffset At { get; set; }
    public string IpHash { get; set; } = string.Empty;
}

public static class UnaykaPolicies
{
    public const string Staff = "Staff";
    public const string Management = "Management";
    public const string Administration = "Administration";
}

public static class PhoneNumber
{
    private const string CountryCode = "258";
    private const int NationalLength = 9;
    private const char MobilePrefix = '8';

    public static string? Normalize(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith(CountryCode, StringComparison.Ordinal) && digits.Length == CountryCode.Length + NationalLength)
        {
            digits = digits[CountryCode.Length..];
        }

        return digits.Length == NationalLength && digits[0] == MobilePrefix ? digits : null;
    }
}

public static class IpHash
{
    private const int HashLength = 16;

    public static string Of(string? address) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address ?? string.Empty)))[..HashLength];
}

public static class ExternalProviders
{
    public const string Google = "Google";
    public const string Facebook = "Facebook";

    public static readonly IReadOnlyList<string> All = [Google, Facebook];

    public static bool IsKnown(string? provider) => provider is not null && All.Contains(provider);

    public static bool IsConfigured(string provider) => provider switch
    {
        Google => AppEnvConfig.GoogleClientId.Length > 0 && AppEnvConfig.GoogleClientSecret.Length > 0,
        Facebook => AppEnvConfig.FacebookAppId.Length > 0 && AppEnvConfig.FacebookAppSecret.Length > 0,
        _ => false,
    };
}

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

public enum SetupOutcome
{
    Created,
    AlreadyConfigured,
    InvalidEmail,
    EmailMismatch,
    PasswordTooShort,
    PasswordMismatch,
}

/// <summary>First administrator typed on the setup screen; email and password are each typed twice.</summary>
public sealed record SetupRequest(string? Email, string? EmailConfirmation, string? Password, string? PasswordConfirmation);

/// <summary>Identity confirmed by an external provider such as Google or Facebook.</summary>
public sealed record ExternalIdentity(string Provider, string Key, string Name, string? Email);

public interface IAccountService
{
    /// <summary>Returns the customer linked to the external identity, creating or linking the account when needed.</summary>
    Task<AccountResult> SignInExternalAsync(ExternalIdentity identity, string ipHash, CancellationToken cancellationToken = default);

    /// <summary>Checks staff email and password.</summary>
    Task<AccountResult> SignInWithPasswordAsync(
        string? email, string? password, string ipHash, CancellationToken cancellationToken = default);

    /// <summary>True once at least one user exists; the app stays on the setup screen until then.</summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates the first administrator when the database has no users.</summary>
    Task<SetupOutcome> CreateFirstAdministratorAsync(SetupRequest request, CancellationToken cancellationToken = default);
}

public sealed class AccountService(
    UnaykaDbContext database,
    IPasswordHasher<User> passwordHasher,
    TimeProvider time) : IAccountService
{
    private const int MaxNameLength = 80;
    private const string DefaultCustomerName = "Cliente";
    private const int MaxFailuresBeforeLockout = 10;
    private const int MinPasswordLength = 10;
    private const string AdministratorName = "Administração";
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

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) =>
        database.Users.AnyAsync(cancellationToken);

    public async Task<SetupOutcome> CreateFirstAdministratorAsync(SetupRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var outcome = ValidateSetup(email, request);
        if (outcome is not null)
        {
            return outcome.Value;
        }

        if (await IsConfiguredAsync(cancellationToken))
        {
            return SetupOutcome.AlreadyConfigured;
        }

        var user = new User { Name = AdministratorName, Email = email, Role = UserRole.Administrator, CreatedAt = time.GetUtcNow() };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
        return SetupOutcome.Created;
    }

    private static SetupOutcome? ValidateSetup(string? email, SetupRequest request)
    {
        if (email is null || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            return SetupOutcome.InvalidEmail;
        }

        if (email != NormalizeEmail(request.EmailConfirmation))
        {
            return SetupOutcome.EmailMismatch;
        }

        if (request.Password is null || request.Password.Length < MinPasswordLength)
        {
            return SetupOutcome.PasswordTooShort;
        }

        return request.Password == request.PasswordConfirmation ? null : SetupOutcome.PasswordMismatch;
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

public static class AccountServiceExtensions
{
    public static IServiceCollection AddUnaykaAccounts(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAccountService, AccountService>();
        return services;
    }
}
