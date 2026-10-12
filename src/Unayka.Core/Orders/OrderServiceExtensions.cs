using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unayka.Core.Orders.Downloads;
using Unayka.Core.Payments;

namespace Unayka.Core.Orders;

public static class OrderServiceExtensions
{
    public static IServiceCollection AddUnaykaOrders(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IOrderPlacement, OrderPlacement>();
        services.AddScoped<IOrderQueries, OrderQueries>();
        services.AddScoped<IDigitalDownloads, DigitalDownloads>();
        return services;
    }
}
