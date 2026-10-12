namespace Unayka.Core.Configuration;

/// <summary>Single typed read point for environment variables.</summary>
public static class AppEnvConfig
{
    private const string ProductionEnvironmentName = "Production";
    private const string DefaultSqliteFilePath = "database.sqlite";
    private const string DefaultLocalUploadsPath = "data/uploads";
    private const string DefaultDataProtectionKeysPath = "data/keys";
    private const string SolutionFileName = "Unayka.slnx";
    private const string SourceFolderName = "src";
    private const int DefaultPostgresPort = 5432;
    private const int DefaultLoginPerMinute = 5;
    private const int DefaultCheckoutPerMinute = 10;
    private const int DefaultWebhookPerMinute = 120;

    public static string AspNetCoreUrls => GetText("ASPNETCORE_URLS");
    public static string AspNetCoreEnvironment => GetText("ASPNETCORE_ENVIRONMENT");
    public static string PublicBaseUrl => GetText("APP_PUBLIC_BASE_URL");

    public static string PostgresHost => GetText("DATABASE_POSTGRESQL_CONNECTION_HOST");
    public static int PostgresPort => GetNumber("DATABASE_POSTGRESQL_CONNECTION_PORT", DefaultPostgresPort);
    public static string PostgresDatabase => GetText("DATABASE_POSTGRESQL_CONNECTION_DATABASE");
    public static string PostgresUser => GetText("DATABASE_POSTGRESQL_CONNECTION_USER");
    public static string PostgresPassword => GetText("DATABASE_POSTGRESQL_CONNECTION_PASSWORD");

    public static string SqliteFilePath =>
        GetTextOrDefault("DATABASE_SQLITE_FILE_PATH", DefaultSqliteFilePath);

    public static string SqliteFullPath =>
        Path.IsPathRooted(SqliteFilePath)
            ? SqliteFilePath
            : Path.Combine(FindRootDirectory(), SqliteFilePath);

    public static string S3Endpoint => GetText("STORAGE_S3_ENDPOINT");
    public static string S3Region => GetText("STORAGE_S3_REGION");
    public static string S3Bucket => GetText("STORAGE_S3_BUCKET");
    public static string S3AccessKey => GetText("STORAGE_S3_ACCESS_KEY");
    public static string S3SecretKey => GetText("STORAGE_S3_SECRET_KEY");
    public static string S3PublicBaseUrl => GetText("STORAGE_S3_PUBLIC_BASE_URL");

    public static string LocalUploadsPath =>
        GetTextOrDefault("STORAGE_LOCAL_UPLOADS_PATH", DefaultLocalUploadsPath);

    public static string DataProtectionKeysPath =>
        GetTextOrDefault("STORAGE_LOCAL_KEYS_PATH", DefaultDataProtectionKeysPath);

    public static string PaySuiteBaseUrl => GetText("PAYMENT_PAYSUITE_BASE_URL");
    public static string PaySuiteApiKey => GetText("PAYMENT_PAYSUITE_API_KEY");
    public static string PaySuiteWebhookSecret => GetText("PAYMENT_PAYSUITE_WEBHOOK_SECRET");
    public static decimal PaySuiteFeePercent => GetDecimal("PAYMENT_PAYSUITE_FEE_PERCENT");
    public static decimal PaySuiteFeeFixed => GetDecimal("PAYMENT_PAYSUITE_FEE_FIXED");
    public static string PayoutPhone => GetText("PAYMENT_PAYOUT_PHONE");

    public static string GoogleClientId => GetText("AUTH_GOOGLE_CLIENT_ID");
    public static string GoogleClientSecret => GetText("AUTH_GOOGLE_CLIENT_SECRET");
    public static string FacebookAppId => GetText("AUTH_FACEBOOK_APP_ID");
    public static string FacebookAppSecret => GetText("AUTH_FACEBOOK_APP_SECRET");
    public static string AdminBootstrapEmail => GetText("AUTH_ADMIN_BOOTSTRAP_EMAIL");
    public static string AuthSeedStaffPassword => GetText("AUTH_SEED_STAFF_PASSWORD");

    public static int RateLimitLoginPerMinute => GetNumber("RATE_LIMIT_LOGIN_PER_MINUTE", DefaultLoginPerMinute);
    public static int RateLimitCheckoutPerMinute => GetNumber("RATE_LIMIT_CHECKOUT_PER_MINUTE", DefaultCheckoutPerMinute);
    public static int RateLimitWebhookPerMinute => GetNumber("RATE_LIMIT_WEBHOOK_PER_MINUTE", DefaultWebhookPerMinute);

    public static bool IsProduction =>
        string.IsNullOrEmpty(AspNetCoreEnvironment)
        || string.Equals(AspNetCoreEnvironment, ProductionEnvironmentName, StringComparison.OrdinalIgnoreCase);

    public static bool UsePostgres =>
        !string.IsNullOrEmpty(PostgresHost) && !string.IsNullOrEmpty(PostgresDatabase);

    public static bool UseS3 =>
        !string.IsNullOrEmpty(S3Endpoint)
        && !string.IsNullOrEmpty(S3Bucket)
        && !string.IsNullOrEmpty(S3AccessKey)
        && !string.IsNullOrEmpty(S3SecretKey);

    public static bool TryGetPostgresPort(out int port) =>
        TryGetNumber("DATABASE_POSTGRESQL_CONNECTION_PORT", out port);

    public static IReadOnlyList<string> GetMissingProductionVariables()
    {
        var missing = new List<string>();
        AddIfEmpty(missing, "APP_PUBLIC_BASE_URL", PublicBaseUrl);
        AddIfEmpty(missing, "PAYMENT_PAYSUITE_BASE_URL", PaySuiteBaseUrl);
        AddIfEmpty(missing, "PAYMENT_PAYSUITE_API_KEY", PaySuiteApiKey);
        AddIfEmpty(missing, "PAYMENT_PAYSUITE_WEBHOOK_SECRET", PaySuiteWebhookSecret);
        AddIfEmpty(missing, "DATABASE_POSTGRESQL_CONNECTION_HOST", PostgresHost);
        AddIfEmpty(missing, "DATABASE_POSTGRESQL_CONNECTION_DATABASE", PostgresDatabase);
        AddIfEmpty(missing, "DATABASE_POSTGRESQL_CONNECTION_USER", PostgresUser);
        AddIfEmpty(missing, "DATABASE_POSTGRESQL_CONNECTION_PASSWORD", PostgresPassword);
        AddIfEmpty(missing, "STORAGE_S3_ENDPOINT", S3Endpoint);
        AddIfEmpty(missing, "STORAGE_S3_BUCKET", S3Bucket);
        AddIfEmpty(missing, "STORAGE_S3_ACCESS_KEY", S3AccessKey);
        AddIfEmpty(missing, "STORAGE_S3_SECRET_KEY", S3SecretKey);
        return missing;
    }

    public static void Validate()
    {
        if (!IsProduction)
        {
            return;
        }

        var missing = GetMissingProductionVariables();
        if (missing.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Missing required environment variables for production: " + string.Join(", ", missing));
    }

    private static void AddIfEmpty(List<string> missing, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
        }
    }

    private static string FindRootDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SourceFolderName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string GetText(string name) =>
        Environment.GetEnvironmentVariable(name)?.Trim() ?? string.Empty;

    private static string GetTextOrDefault(string name, string fallback)
    {
        var value = GetText(name);
        return value.Length == 0 ? fallback : value;
    }

    private static bool TryGetNumber(string name, out int value) =>
        int.TryParse(GetText(name), out value);

    private static int GetNumber(string name, int fallback) =>
        TryGetNumber(name, out var value) ? value : fallback;

    private static decimal GetDecimal(string name) =>
        decimal.TryParse(GetText(name), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
}
