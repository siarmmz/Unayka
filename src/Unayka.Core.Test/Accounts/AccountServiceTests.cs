using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Accounts;

[Collection(SharedEnvironment.Name)]
public sealed class AccountServiceTests : IDisposable
{
    private const string IpHash = "ip";

    private readonly TestDatabase _database = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        _service = new AccountService(_database.Context, new PasswordHasher<User>(), _time);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task First_external_sign_in_creates_a_customer_and_the_next_one_reuses_it()
    {
        var identity = new ExternalIdentity(ExternalProviders.Google, "g-123", "Amélia", "Amelia@Mail.com");

        var first = await _service.SignInExternalAsync(identity, IpHash);
        var second = await _service.SignInExternalAsync(identity, IpHash);

        Assert.True(first.Succeeded);
        Assert.Equal(UserRole.Customer, first.User!.Role);
        Assert.Equal("amelia@mail.com", first.User.Email);
        Assert.Equal(first.User.Id, second.User!.Id);
        Assert.Equal(1, await _database.Context.Users.CountAsync());
    }

    [Fact]
    public async Task Same_email_from_another_provider_signs_into_the_same_customer()
    {
        var google = await _service.SignInExternalAsync(new ExternalIdentity(ExternalProviders.Google, "g-1", "Amélia", "amelia@mail.com"), IpHash);

        var facebook = await _service.SignInExternalAsync(new ExternalIdentity(ExternalProviders.Facebook, "f-1", "Amélia", "amelia@mail.com"), IpHash);

        Assert.Equal(google.User!.Id, facebook.User!.Id);
    }

    [Theory]
    [InlineData("Telegram", "key")]
    [InlineData("Google", "")]
    public async Task Unknown_provider_or_missing_key_is_rejected(string provider, string key)
    {
        var result = await _service.SignInExternalAsync(new ExternalIdentity(provider, key, "Amélia", null), IpHash);

        Assert.Equal(AccountOutcome.InvalidIdentity, result.Outcome);
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
}
