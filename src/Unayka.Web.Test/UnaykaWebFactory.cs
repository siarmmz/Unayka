using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Unayka.Core.Accounts;
using Unayka.Core.Data;

namespace Unayka.Web.Test;

public sealed class UnaykaWebFactory : WebApplicationFactory<Program>
{
    public const string StaffPassword = "teste-unayka-2026";

    private const string EnvironmentVariableName = "ASPNETCORE_ENVIRONMENT";
    private const string DevelopmentEnvironment = "Development";
    private const string SqlitePathVariable = "DATABASE_SQLITE_FILE_PATH";
    private const string KeysPathVariable = "STORAGE_LOCAL_KEYS_PATH";
    private const string LoginRateLimitVariable = "RATE_LIMIT_LOGIN_PER_MINUTE";
    private const string TestLoginRateLimit = "100";
    private const string DatabaseFileName = "database.sqlite";

    private static readonly (string Name, string Email, UserRole Role)[] Staff =
    [
        ("Administração", "admin@unayka.co.mz", UserRole.Administrator),
        ("Gerente", "gerente@unayka.co.mz", UserRole.Manager),
        ("Caixa", "caixa@unayka.co.mz", UserRole.Cashier),
    ];

    private readonly string _workFolder = Path.Combine(Path.GetTempPath(), $"unayka-web-test-{Guid.NewGuid():N}");

    public UnaykaWebFactory()
    {
        Directory.CreateDirectory(_workFolder);
        var databasePath = Path.Combine(_workFolder, "test.sqlite");
        File.Copy(StoreDatabasePath(), databasePath);
        Environment.SetEnvironmentVariable(EnvironmentVariableName, DevelopmentEnvironment);
        Environment.SetEnvironmentVariable(SqlitePathVariable, databasePath);
        Environment.SetEnvironmentVariable(KeysPathVariable, Path.Combine(_workFolder, "keys"));
        Environment.SetEnvironmentVariable(LoginRateLimitVariable, TestLoginRateLimit);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<UnaykaDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        foreach (var (name, email, role) in Staff.Where(staff => !database.Users.Any(user => user.Email == staff.Email)))
        {
            var user = new User { Name = name, Email = email, Role = role, CreatedAt = DateTimeOffset.UtcNow };
            user.PasswordHash = hasher.HashPassword(user, StaffPassword);
            database.Users.Add(user);
        }

        database.SaveChanges();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            TryDeleteWorkFolder();
        }
    }

    private static string StoreDatabasePath()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, DatabaseFileName)))
        {
            folder = folder.Parent;
        }

        return folder is null ? throw new FileNotFoundException(DatabaseFileName) : Path.Combine(folder.FullName, DatabaseFileName);
    }

    private void TryDeleteWorkFolder()
    {
        try
        {
            Directory.Delete(_workFolder, true);
        }
        catch (IOException)
        {
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SharedWebServer : ICollectionFixture<UnaykaWebFactory>
{
    public const string Name = "Web";
}
