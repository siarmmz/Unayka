using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Unayka.Core.Carts;

public static class CartServiceExtensions
{
    public static IServiceCollection AddUnaykaCarts(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICartSummary, CartSummary>();
        services.AddScoped<ICartService, CartService>();
        return services;
    }
}
