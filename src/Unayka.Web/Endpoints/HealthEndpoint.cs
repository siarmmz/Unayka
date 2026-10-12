using Unayka.Core.Configuration;

namespace Unayka.Web.Endpoints;

public static class HealthEndpoint
{
    private const string Route = "/health";
    private const string OkStatus = "ok";
    private const string PostgresName = "postgres";
    private const string SqliteName = "sqlite";
    private const string S3Name = "s3";
    private const string LocalName = "local";

    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, () => Results.Ok(new
        {
            status = OkStatus,
            database = AppEnvConfig.UsePostgres ? PostgresName : SqliteName,
            storage = AppEnvConfig.UseS3 ? S3Name : LocalName,
        }));
        return endpoints;
    }
}
