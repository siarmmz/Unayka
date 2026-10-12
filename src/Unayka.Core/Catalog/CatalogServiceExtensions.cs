using Microsoft.Extensions.DependencyInjection;

namespace Unayka.Core.Catalog;

public static class CatalogServiceExtensions
{
    public static IServiceCollection AddUnaykaCatalog(this IServiceCollection services)
    {
        services.AddScoped<ICatalogQuery, CatalogQuery>();
        return services;
    }
}
