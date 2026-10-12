using Microsoft.Extensions.DependencyInjection;

namespace Unayka.Core.Locations;

public static class LocationServiceExtensions
{
    public static IServiceCollection AddUnaykaLocations(this IServiceCollection services)
    {
        services.AddScoped<ILocationDirectory, LocationDirectory>();
        return services;
    }
}
