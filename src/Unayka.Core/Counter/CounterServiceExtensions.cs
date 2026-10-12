using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Unayka.Core.Counter;

public static class CounterServiceExtensions
{
    public static IServiceCollection AddUnaykaCounter(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICounterShifts, CounterShifts>();
        services.AddScoped<ICounterSales, CounterSales>();
        services.AddScoped<ICounterPreparation, CounterPreparation>();
        return services;
    }
}
