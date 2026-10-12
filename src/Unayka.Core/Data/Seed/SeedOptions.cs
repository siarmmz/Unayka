using Unayka.Core.Configuration;

namespace Unayka.Core.Data.Seed;

/// <summary>Controls what the seeder creates. Staff are only seeded when a password is available.</summary>
public sealed record SeedOptions(string StaffPassword, string AdminEmail, bool IncludeDemoData)
{
    private const string DevelopmentStaffPassword = "unayka2026";
    private const string DefaultAdminEmail = "admin@unayka.co.mz";

    public bool SeedStaff => StaffPassword.Length > 0;

    public static SeedOptions FromEnvironment()
    {
        var password = AppEnvConfig.AuthSeedStaffPassword;
        if (password.Length == 0 && !AppEnvConfig.IsProduction)
        {
            password = DevelopmentStaffPassword;
        }

        var adminEmail = AppEnvConfig.AdminBootstrapEmail;
        return new SeedOptions(
            password,
            adminEmail.Length == 0 ? DefaultAdminEmail : adminEmail,
            !AppEnvConfig.IsProduction);
    }
}
