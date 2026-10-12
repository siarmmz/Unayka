using Microsoft.AspNetCore.Mvc.Testing;

namespace Unayka.Web.Test;

public sealed class UnaykaWebFactory : WebApplicationFactory<Program>
{
    private const string EnvironmentVariableName = "ASPNETCORE_ENVIRONMENT";
    private const string DevelopmentEnvironment = "Development";
    private const string SqlitePathVariable = "DATABASE_SQLITE_FILE_PATH";
    private const string KeysPathVariable = "STORAGE_LOCAL_KEYS_PATH";

    private readonly string _workFolder = Path.Combine(Path.GetTempPath(), $"unayka-web-test-{Guid.NewGuid():N}");

    public UnaykaWebFactory()
    {
        Directory.CreateDirectory(_workFolder);
        Environment.SetEnvironmentVariable(EnvironmentVariableName, DevelopmentEnvironment);
        Environment.SetEnvironmentVariable(SqlitePathVariable, Path.Combine(_workFolder, "test.sqlite"));
        Environment.SetEnvironmentVariable(KeysPathVariable, Path.Combine(_workFolder, "keys"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            TryDeleteWorkFolder();
        }
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
