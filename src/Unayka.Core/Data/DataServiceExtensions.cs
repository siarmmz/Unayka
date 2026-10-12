using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Unayka.Core.Audit;
using Unayka.Core.Configuration;
using Unayka.Core.Data.Seed;

namespace Unayka.Core.Data;

public static class DataServiceExtensions
{
    public static IServiceCollection AddUnaykaData(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContextFactory<UnaykaDbContext>(ConfigureProvider);
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<UnaykaDbContext>>().CreateDbContext());
        services.AddScoped<IAuditLog, AuditLog>();
        return services;
    }

    public static async Task InitializeUnaykaDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<UnaykaDbContext>();
        await MigrateAsync(database);
        await DatabaseSeeder.SeedAsync(database, SeedOptions.FromEnvironment(), scope.ServiceProvider.GetRequiredService<TimeProvider>());
    }

    private static async Task MigrateAsync(UnaykaDbContext database)
    {
        if (AppEnvConfig.UsePostgres)
        {
            await database.Database.EnsureCreatedAsync();
            return;
        }

        await database.Database.MigrateAsync();
    }

    public static void ConfigureProvider(DbContextOptionsBuilder options)
    {
        if (AppEnvConfig.UsePostgres)
        {
            options.UseNpgsql(BuildPostgresConnectionString());
            return;
        }

        options.UseSqlite($"Data Source={AppEnvConfig.SqliteFullPath}");
    }

    private static string BuildPostgresConnectionString() =>
        new NpgsqlConnectionStringBuilder
        {
            Host = AppEnvConfig.PostgresHost,
            Port = AppEnvConfig.PostgresPort,
            Database = AppEnvConfig.PostgresDatabase,
            Username = AppEnvConfig.PostgresUser,
            Password = AppEnvConfig.PostgresPassword,
        }.ConnectionString;
}
