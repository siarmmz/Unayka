namespace Unayka.Core.Configuration;

/// <summary>Single typed read point for environment variables.</summary>
public static class AppEnvConfig
{
    private const string ProductionEnvironmentName = "Production";
    private const int UndefinedNumber = -1;
    private const int DefaultLoginPerMinute = 5;
    private const int DefaultOtpPerHour = 5;
    private const int DefaultCheckoutPerMinute = 10;
    private const int DefaultWebhookPerMinute = 120;

    public static string AspNetCoreUrls => GetText("ASPNETCORE_URLS");
    public static string AspNetCoreEnvironment => GetText("ASPNETCORE_ENVIRONMENT");
    public static string DotNetEnvironment => GetText("DOTNET_ENVIRONMENT");
    public static string PublicBaseUrl => GetText("PUBLIC_BASE_URL");

    public static string PostgresConnectionString => GetText("POSTGRES_SQL_CONNECTION_STRING");
    public static string PostgresHost => GetText("POSTGRES_HOST");
    public static int PostgresPort => GetNumber("POSTGRES_PORT", UndefinedNumber);
    public static string PostgresDatabase => GetText("POSTGRES_DATABASE");
    public static string PostgresUser => GetText("POSTGRES_USER");
    public static string PostgresPassword => GetText("POSTGRES_PASSWORD");
    public static string SqlitePath => GetText("SQLITE_PATH");

    public static string S3Endpoint => GetText("S3_ENDPOINT");
    public static string S3Region => GetText("S3_REGION");
    public static string S3Bucket => GetText("S3_BUCKET");
    public static string S3AccessKey => GetText("S3_ACCESS_KEY");
    public static string S3SecretKey => GetText("S3_SECRET_KEY");
    public static string S3PublicBaseUrl => GetText("S3_PUBLIC_BASE_URL");
    public static string LocalUploadsPath => GetText("LOCAL_UPLOADS_PATH");

    public static string PaySuiteBaseUrl => GetText("PAYSUITE_BASE_URL");
    public static string PaySuiteApiKey => GetText("PAYSUITE_API_KEY");
    public static string PaySuiteWebhookSecret => GetText("PAYSUITE_WEBHOOK_SECRET");
    public static decimal PaySuiteFeePercent => GetDecimal("PAYSUITE_FEE_PERCENT");
    public static decimal PaySuiteFeeFixed => GetDecimal("PAYSUITE_FEE_FIXED");
    public static string PayoutPhone => GetText("PAYOUT_PHONE");

    public static string GoogleClientId => GetText("GOOGLE_CLIENT_ID");
    public static string GoogleClientSecret => GetText("GOOGLE_CLIENT_SECRET");
    public static string SmsProvider => GetText("SMS_PROVIDER");
    public static string SmsApiKey => GetText("SMS_API_KEY");
    public static string AdminBootstrapEmail => GetText("ADMIN_BOOTSTRAP_EMAIL");

    public static int RateLimitLoginPerMinute => GetNumber("RATE_LIMIT_LOGIN_PER_MINUTE", DefaultLoginPerMinute);
    public static int RateLimitOtpPerHour => GetNumber("RATE_LIMIT_OTP_PER_HOUR", DefaultOtpPerHour);
    public static int RateLimitCheckoutPerMinute => GetNumber("RATE_LIMIT_CHECKOUT_PER_MINUTE", DefaultCheckoutPerMinute);
    public static int RateLimitWebhookPerMinute => GetNumber("RATE_LIMIT_WEBHOOK_PER_MINUTE", DefaultWebhookPerMinute);

    public static bool IsProduction =>
        string.IsNullOrEmpty(AspNetCoreEnvironment)
        || string.Equals(AspNetCoreEnvironment, ProductionEnvironmentName, StringComparison.OrdinalIgnoreCase);

    public static bool UsePostgres =>
        !string.IsNullOrEmpty(PostgresConnectionString)
        || (!string.IsNullOrEmpty(PostgresHost) && !string.IsNullOrEmpty(PostgresDatabase));

    public static bool UseS3 =>
        !string.IsNullOrEmpty(S3Endpoint)
        && !string.IsNullOrEmpty(S3Bucket)
        && !string.IsNullOrEmpty(S3AccessKey)
        && !string.IsNullOrEmpty(S3SecretKey);

    public static bool TryGetPostgresPort(out int port) => TryGetNumber("POSTGRES_PORT", out port);

    public static IReadOnlyList<string> GetMissingProductionVariables()
    {
        var missing = new List<string>();
        AddIfEmpty(missing, "PUBLIC_BASE_URL", PublicBaseUrl);
        AddIfEmpty(missing, "PAYSUITE_BASE_URL", PaySuiteBaseUrl);
        AddIfEmpty(missing, "PAYSUITE_API_KEY", PaySuiteApiKey);
        AddIfEmpty(missing, "PAYSUITE_WEBHOOK_SECRET", PaySuiteWebhookSecret);

        if (!UsePostgres)
        {
            missing.Add("POSTGRES_SQL_CONNECTION_STRING or POSTGRES_HOST and POSTGRES_DATABASE");
        }

        if (!UseS3)
        {
            missing.Add("S3_ENDPOINT, S3_BUCKET, S3_ACCESS_KEY and S3_SECRET_KEY");
        }

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
            "Missing required environment variables for production: " + string.Join("; ", missing));
    }

    private static void AddIfEmpty(List<string> missing, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
        }
    }

    private static string GetText(string name) =>
        Environment.GetEnvironmentVariable(name)?.Trim() ?? string.Empty;

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
