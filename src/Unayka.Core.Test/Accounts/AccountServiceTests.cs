using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Accounts;

[Collection(SharedEnvironment.Name)]
public sealed class AccountServiceTests : IDisposable
{
    private const string Phone = "841234567";
    private const string IpHash = "ip";

    private readonly TestDatabase _database = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
    private readonly CapturingSender _sender = new();
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        _service = new AccountService(_database.Context, new PasswordHasher<User>(), _sender, _time);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Code_is_stored_hashed_and_sent()
    {
        var outcome = await _service.RequestCodeAsync(Phone, IpHash);

        var stored = await _database.Context.OtpCodes.SingleAsync();
        Assert.Equal(AccountOutcome.Succeeded, outcome);
        Assert.NotEqual(_sender.LastCode, stored.CodeHash);
        Assert.Equal(_time.GetUtcNow() + OtpCodes.ValidFor, stored.ExpiresAt);
    }

    [Fact]
    public async Task New_phone_must_provide_a_name_and_then_gets_a_customer_account()
    {
        await _service.RequestCodeAsync(Phone, IpHash);

        var withoutName = await _service.VerifyCodeAsync(Phone, _sender.LastCode, null, IpHash);
        var withName = await _service.VerifyCodeAsync(Phone, _sender.LastCode, "Amélia", IpHash);

        Assert.Equal(AccountOutcome.NameRequired, withoutName.Outcome);
        Assert.Equal(AccountOutcome.Succeeded, withName.Outcome);
        Assert.Equal(UserRole.Customer, withName.User!.Role);
    }

    [Fact]
    public async Task Code_cannot_be_used_twice()
    {
        await _service.RequestCodeAsync(Phone, IpHash);
        await _service.VerifyCodeAsync(Phone, _sender.LastCode, "Amélia", IpHash);

        var second = await _service.VerifyCodeAsync(Phone, _sender.LastCode, "Amélia", IpHash);

        Assert.Equal(AccountOutcome.InvalidCode, second.Outcome);
    }

    [Fact]
    public async Task Expired_code_is_rejected()
    {
        await _service.RequestCodeAsync(Phone, IpHash);
        _time.Advance(OtpCodes.ValidFor + TimeSpan.FromSeconds(1));

        var result = await _service.VerifyCodeAsync(Phone, _sender.LastCode, "Amélia", IpHash);

        Assert.Equal(AccountOutcome.CodeExpired, result.Outcome);
    }

    [Fact]
    public async Task Code_is_blocked_after_too_many_wrong_attempts()
    {
        await _service.RequestCodeAsync(Phone, IpHash);
        for (var attempt = 0; attempt < OtpCodes.MaxAttempts; attempt++)
        {
            await _service.VerifyCodeAsync(Phone, "WRONG1", "Amélia", IpHash);
        }

        var result = await _service.VerifyCodeAsync(Phone, _sender.LastCode, "Amélia", IpHash);

        Assert.Equal(AccountOutcome.TooManyAttempts, result.Outcome);
        Assert.Equal(OtpCodes.MaxAttempts + 1, await _database.Context.LoginAttempts.CountAsync(login => !login.Succeeded));
    }

    [Fact]
    public async Task Staff_sign_in_checks_the_password_and_records_the_attempt()
    {
        var user = new User { Name = "Caixa", Email = "caixa@unayka.co.mz", Role = UserRole.Cashier, CreatedAt = _time.GetUtcNow() };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "secret-pass");
        _database.Context.Users.Add(user);
        await _database.Context.SaveChangesAsync();

        var wrong = await _service.SignInWithPasswordAsync("caixa@unayka.co.mz", "nope", IpHash);
        var right = await _service.SignInWithPasswordAsync("CAIXA@unayka.co.mz", "secret-pass", IpHash);

        Assert.Equal(AccountOutcome.InvalidCredentials, wrong.Outcome);
        Assert.True(right.Succeeded);
        Assert.Equal(2, await _database.Context.LoginAttempts.CountAsync());
    }

    private sealed class CapturingSender : IOtpSender
    {
        public string LastCode { get; private set; } = string.Empty;

        public Task SendAsync(string phone, string code, CancellationToken cancellationToken = default)
        {
            LastCode = code;
            return Task.CompletedTask;
        }
    }
}
