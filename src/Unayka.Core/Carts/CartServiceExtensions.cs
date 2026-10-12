using Microsoft.Extensions.DependencyInjection;

namespace Unayka.Core.Carts;

public static class CartServiceExtensions
{
    public static IServiceCollection AddUnaykaCarts(this IServiceCollection services)
    {
        services.AddScoped<ICartSummary, CartSummary>();
        return services;
    }
}
