using Unayka.Core.Configuration;

namespace Unayka.Core.Test.Configuration;

[Collection(SharedEnvironment.Name)]
public sealed class AppEnvConfigTests : IDisposable
{
    private readonly List<string> _touched = [];

    [Fact]
    public void Text_returns_empty_when_not_set()
    {
        Set("S3_BUCKET", null);

        Assert.Equal(string.Empty, AppEnvConfig.S3Bucket);
    }

    [Fact]
    public void Number_returns_documented_default_when_not_set()
    {
        Set("RATE_LIMIT_LOGIN_PER_MINUTE", null);

        Assert.Equal(5, AppEnvConfig.RateLimitLoginPerMinute);
    }

    [Fact]
    public void Number_returns_minus_one_when_port_not_set()
    {
        Set("POSTGRES_PORT", null);

        Assert.Equal(-1, AppEnvConfig.PostgresPort);
        Assert.False(AppEnvConfig.TryGetPostgresPort(out _));
    }

    [Fact]
    public void TryGetPostgresPort_reads_defined_value()
    {
        Set("POSTGRES_PORT", "5433");

        Assert.True(AppEnvConfig.TryGetPostgresPort(out var port));
        Assert.Equal(5433, port);
    }

    [Fact]
    public void UsePostgres_is_true_with_connection_string()
    {
        Set("POSTGRES_SQL_CONNECTION_STRING", "Host=localhost;Database=unayka");
        Set("POSTGRES_HOST", null);

        Assert.True(AppEnvConfig.UsePostgres);
    }

    [Fact]
    public void UsePostgres_is_false_when_nothing_is_set()
    {
        Set("POSTGRES_SQL_CONNECTION_STRING", null);
        Set("POSTGRES_HOST", null);
        Set("POSTGRES_DATABASE", null);

        Assert.False(AppEnvConfig.UsePostgres);
    }

    [Fact]
    public void UseS3_needs_all_four_values()
    {
        Set("S3_ENDPOINT", "https://s3.example.com");
        Set("S3_BUCKET", "unayka");
        Set("S3_ACCESS_KEY", "key");
        Set("S3_SECRET_KEY", null);

        Assert.False(AppEnvConfig.UseS3);

        Set("S3_SECRET_KEY", "secret");

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
        Set("PAYSUITE_API_KEY", null);
        Set("POSTGRES_SQL_CONNECTION_STRING", null);
        Set("POSTGRES_HOST", null);

        var error = Assert.Throws<InvalidOperationException>(AppEnvConfig.Validate);

        Assert.Contains("PAYSUITE_API_KEY", error.Message);
        Assert.Contains("POSTGRES_SQL_CONNECTION_STRING", error.Message);
    }

    [Fact]
    public void Validate_does_not_leak_secret_values()
    {
        Set("ASPNETCORE_ENVIRONMENT", "Production");
        Set("PAYSUITE_WEBHOOK_SECRET", "super-secret-value");
        Set("PAYSUITE_API_KEY", null);

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
