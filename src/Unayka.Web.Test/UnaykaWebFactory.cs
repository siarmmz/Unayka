using Microsoft.AspNetCore.Mvc.Testing;

namespace Unayka.Web.Test;

public sealed class UnaykaWebFactory : WebApplicationFactory<Program>
{
    private const string EnvironmentVariableName = "ASPNETCORE_ENVIRONMENT";
    private const string DevelopmentEnvironment = "Development";

    public UnaykaWebFactory()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableName, DevelopmentEnvironment);
    }
}
