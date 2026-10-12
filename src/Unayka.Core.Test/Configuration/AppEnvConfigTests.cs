using Unayka.Core.Configuration;

namespace Unayka.Core.Test.Configuration;

[Collection(SharedEnvironment.Name)]
public sealed class AppEnvConfigTests : IDisposable
{
    private const string PostgresHost = "DATABASE_POSTGRESQL_CONNECTION_HOST";
    private const string PostgresPort = "DATABASE_POSTGRESQL_CONNECTION_PORT";
    private const string PostgresDatabase = "DATABASE_POSTGRESQL_CONNECTION_DATABASE";
    private const string SqliteFilePath = "DATABASE_SQLITE_FILE_PATH";

    private readonly List<string> _touched = [];

    [Fact]
    public void Text_returns_empty_when_not_set()
    {
        Set("STORAGE_S3_BUCKET", null);

        Assert.Equal(string.Empty, AppEnvConfig.S3Bucket);
    }

    [Fact]
    public void Number_returns_documented_default_when_not_set()
    {
        Set("RATE_LIMIT_LOGIN_PER_MINUTE", null);

        Assert.Equal(5, AppEnvConfig.RateLimitLoginPerMinute);
    }

    [Fact]
    public void Postgres_port_defaults_to_5432_and_reports_it_was_not_defined()
    {
        Set(PostgresPort, null);

        Assert.Equal(5432, AppEnvConfig.PostgresPort);
        Assert.False(AppEnvConfig.TryGetPostgresPort(out _));
    }

    [Fact]
    public void TryGetPostgresPort_reads_defined_value()
    {
        Set(PostgresPort, "5433");

        Assert.True(AppEnvConfig.TryGetPostgresPort(out var port));
        Assert.Equal(5433, port);
    }

    [Fact]
    public void UsePostgres_needs_host_and_database()
    {
        Set(PostgresHost, "localhost");
        Set(PostgresDatabase, null);

        Assert.False(AppEnvConfig.UsePostgres);

        Set(PostgresDatabase, "unayka");

        Assert.True(AppEnvConfig.UsePostgres);
    }

    [Fact]
    public void Sqlite_defaults_to_database_sqlite_at_repository_root()
    {
        Set(SqliteFilePath, null);

        Assert.Equal("database.sqlite", AppEnvConfig.SqliteFilePath);
        Assert.Equal("database.sqlite", Path.GetFileName(AppEnvConfig.SqliteFullPath));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(AppEnvConfig.SqliteFullPath)!, "src", "Unayka.slnx")));
    }

    [Fact]
    public void Sqlite_path_can_be_overridden_with_absolute_path()
    {
        var path = Path.Combine(Path.GetTempPath(), "custom.sqlite");
        Set(SqliteFilePath, path);

        Assert.Equal(path, AppEnvConfig.SqliteFullPath);
    }

    [Fact]
    public void UseS3_needs_endpoint_bucket_and_both_keys()
    {
        Set("STORAGE_S3_ENDPOINT", "https://s3.example.com");
        Set("STORAGE_S3_BUCKET", "unayka");
        Set("STORAGE_S3_ACCESS_KEY", "key");
        Set("STORAGE_S3_SECRET_KEY", null);

        Assert.False(AppEnvConfig.UseS3);

        Set("STORAGE_S3_SECRET_KEY", "secret");

        Assert.True(AppEnvConfig.UseS3);
    }

    [Fact]
    public void Validate_does_nothing_outside_production()
    {
        Set("ASPNETCORE_ENVIRONMENT", "Development");

        AppEnvConfig.Validate();
    }

    [Fact]
    public void Validate_lists_missing_variables_in_production()
    {
        Set("ASPNETCORE_ENVIRONMENT", "Production");
        Set("PAYMENT_PAYSUITE_API_KEY", null);
        Set(PostgresHost, null);

        var error = Assert.Throws<InvalidOperationException>(AppEnvConfig.Validate);

        Assert.Contains("PAYMENT_PAYSUITE_API_KEY", error.Message);
        Assert.Contains(PostgresHost, error.Message);
    }

    [Fact]
    public void Validate_does_not_leak_secret_values()
    {
        Set("ASPNETCORE_ENVIRONMENT", "Production");
        Set("PAYMENT_PAYSUITE_WEBHOOK_SECRET", "super-secret-value");
        Set("PAYMENT_PAYSUITE_API_KEY", null);

        var error = Assert.Throws<InvalidOperationException>(AppEnvConfig.Validate);

        Assert.DoesNotContain("super-secret-value", error.Message);
    }

    public void Dispose()
    {
        foreach (var name in _touched)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    private void Set(string name, string? value)
    {
        _touched.Add(name);
        Environment.SetEnvironmentVariable(name, value);
    }
}
