using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Configuration;
using Unayka.Core.Data;

namespace Unayka.Core.Accounts;

public sealed class AccountService(
    UnaykaDbContext database,
    IPasswordHasher<User> passwordHasher,
    IOtpSender otpSender,
    TimeProvider time) : IAccountService
{
    private const int MaxNameLength = 80;
    private const int MaxFailuresBeforeLockout = 10;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CodeRequestWindow = TimeSpan.FromHours(1);

    public async Task<AccountOutcome> RequestCodeAsync(
        string? phone, string ipHash, CancellationToken cancellationToken = default)
    {
        var normalized = PhoneNumber.Normalize(phone);
        if (normalized is null)
        {
            return AccountOutcome.InvalidPhone;
        }

        var now = time.GetUtcNow();
        var since = now - CodeRequestWindow;
        var recent = await database.OtpCodes.CountAsync(
            code => code.Phone == normalized && code.CreatedAt > since, cancellationToken);
        if (recent >= AppEnvConfig.RateLimitOtpPerHour)
        {
            return AccountOutcome.TooManyRequests;
        }

        var plainCode = OtpCodes.Generate();
        database.OtpCodes.Add(new OtpCode
        {
            Phone = normalized,
            CodeHash = OtpCodes.Hash(normalized, plainCode),
            CreatedAt = now,
            ExpiresAt = now + OtpCodes.ValidFor,
        });
        await database.SaveChangesAsync(cancellationToken);
        await otpSender.SendAsync(normalized, plainCode, cancellationToken);
        return AccountOutcome.Succeeded;
    }

    public async Task<AccountResult> VerifyCodeAsync(
        string? phone, string? code, string? name, string ipHash, CancellationToken cancellationToken = default)
    {
        var normalized = PhoneNumber.Normalize(phone);
        if (normalized is null)
        {
            return new AccountResult(AccountOutcome.InvalidPhone);
        }

        var otp = await database.OtpCodes
            .Where(candidate => candidate.Phone == normalized && candidate.ConsumedAt == null)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var now = time.GetUtcNow();
        var rejection = Reject(otp, now);
        if (rejection is not null)
        {
            return await FailAsync(normalized, ipHash, rejection.Value, cancellationToken);
        }

        if (!OtpCodes.Matches(normalized, code ?? string.Empty, otp!.CodeHash))
        {
            otp.Attempts++;
            return await FailAsync(normalized, ipHash, AccountOutcome.InvalidCode, cancellationToken);
        }

        var user = await database.Users.FirstOrDefaultAsync(candidate => candidate.Phone == normalized, cancellationToken);
        if (user is null && string.IsNullOrWhiteSpace(name))
        {
            return new AccountResult(AccountOutcome.NameRequired);
        }

        user ??= CreateCustomer(normalized, name!, now);
        otp.ConsumedAt = now;
        user.LastLoginAt = now;
        RecordAttempt(normalized, true, ipHash, now);
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

    private static AccountOutcome? Reject(OtpCode? otp, DateTimeOffset now)
    {
        if (otp is null)
        {
            return AccountOutcome.InvalidCode;
        }

        if (otp.ExpiresAt <= now)
        {
            return AccountOutcome.CodeExpired;
        }

        return otp.Attempts >= OtpCodes.MaxAttempts ? AccountOutcome.TooManyAttempts : null;
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

    private User CreateCustomer(string phone, string name, DateTimeOffset now)
    {
        var trimmed = name.Trim();
        var user = new User
        {
            Name = trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed,
            Phone = phone,
            Role = UserRole.Customer,
            CreatedAt = now,
        };
        database.Users.Add(user);
        return user;
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
