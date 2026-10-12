using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Unayka.Core.Accounts;

public static class AccountServiceExtensions
{
    public static IServiceCollection AddUnaykaAccounts(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IDevelopmentOtpInbox, DevelopmentOtpInbox>();
        services.AddSingleton<IOtpSender, DevelopmentOtpSender>();
        services.AddScoped<IAccountService, AccountService>();
        return services;
    }
}
