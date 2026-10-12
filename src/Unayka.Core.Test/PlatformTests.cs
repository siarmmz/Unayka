using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Configuration;
using Unayka.Core.Presentation;

namespace Unayka.Core.Test;

public sealed class AuditLogTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Write_inserts_an_entry()
    {
        var log = new AuditLog(_database.Context, TimeProvider.System);

        await log.WriteAsync(1, "product.update", "Product", "7", "Price changed");

        var entry = await _database.Context.AuditEntries.SingleAsync();
        Assert.Equal("product.update", entry.Action);
        Assert.Equal(1, entry.ActorUserId);
    }

    [Fact]
    public async Task Modifying_an_entry_is_rejected()
    {
        await new AuditLog(_database.Context, TimeProvider.System).WriteAsync(null, "a", "T", "1", "s");
        var entry = await _database.Context.AuditEntries.SingleAsync();

        entry.Summary = "changed";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_an_entry_is_rejected()
    {
        await new AuditLog(_database.Context, TimeProvider.System).WriteAsync(null, "a", "T", "1", "s");
        var entry = await _database.Context.AuditEntries.SingleAsync();

        _database.Context.AuditEntries.Remove(entry);

        Assert.Throws<InvalidOperationException>(() => _database.Context.SaveChanges());
    }
}

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

public sealed class FormattersTests
{
    [Fact]
    public void FormatMoney_uses_comma_decimals_and_currency()
    {
        Assert.Equal("1\u00A0250,50\u00A0MZN", Formatters.FormatMoney(125050));
    }

    [Fact]
    public void FormatMoney_formats_zero()
    {
        Assert.Equal("0,00\u00A0MZN", Formatters.FormatMoney(0));
    }

    [Theory]
    [InlineData("841234567")]
    [InlineData("+258 84 123 4567")]
    [InlineData("258841234567")]
    public void FormatPhone_normalizes_mozambican_numbers(string value)
    {
        Assert.Equal("+258 84 123 4567", Formatters.FormatPhone(value));
    }

    [Fact]
    public void FormatPhone_keeps_unknown_values()
    {
        Assert.Equal("12345", Formatters.FormatPhone("12345"));
    }

    [Fact]
    public void FormatDate_uses_maputo_time()
    {
        var value = new DateTimeOffset(2026, 10, 12, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal("13/10/2026", Formatters.FormatDate(value));
    }
}

public sealed class SafeTextTests
{
    [Fact]
    public void ToHtml_escapes_markup()
    {
        Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;", SafeText.ToHtml("<script>alert(1)</script>"));
    }

    [Fact]
    public void ToHtml_without_format_escapes_inline_tags()
    {
        Assert.Equal("&lt;b&gt;x&lt;/b&gt;", SafeText.ToHtml("<b>x</b>"));
    }

    [Fact]
    public void ToHtml_with_format_allows_only_inline_tags()
    {
        Assert.Equal("<b>x</b><br>&lt;a href=&quot;/&quot;&gt;", SafeText.ToHtml("<b>x</b><br><a href=\"/\">", format: true));
    }

    [Fact]
    public void ToHtml_returns_empty_for_null()
    {
        Assert.Equal(string.Empty, SafeText.ToHtml(null));
    }
}

public sealed class SafeUrlTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.example.com")]
    [InlineData("/\evil.example.com")]
    [InlineData("java\nscript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void ToUrl_rejects_unsafe_values(string? value)
    {
        Assert.Equal(SafeUrl.FallbackUrl, SafeUrl.ToUrl(value));
    }

    [Theory]
    [InlineData("/contact")]
    [InlineData("#top")]
    [InlineData("mailto:ola@unayka.co.mz")]
    [InlineData("tel:+258841234567")]
    [InlineData("https://example.com/page")]
    public void ToUrl_keeps_safe_values(string value)
    {
        Assert.Equal(value, SafeUrl.ToUrl(value));
    }

    [Theory]
    [InlineData("/contact", SafeUrl.SelfTarget)]
    [InlineData("mailto:ola@unayka.co.mz", SafeUrl.SelfTarget)]
    [InlineData("tel:+258841234567", SafeUrl.SelfTarget)]
    [InlineData("https://example.com", SafeUrl.BlankTarget)]
    [InlineData("javascript:alert(1)", SafeUrl.SelfTarget)]
    public void ToTarget_matches_url_kind(string value, string expected)
    {
        Assert.Equal(expected, SafeUrl.ToTarget(value));
    }

    [Fact]
    public void ToTarget_treats_public_host_as_local()
    {
        Assert.Equal(SafeUrl.SelfTarget, SafeUrl.ToTarget("https://unayka.co.mz/about", "https://unayka.co.mz"));
    }
}
