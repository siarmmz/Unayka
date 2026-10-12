using Microsoft.Extensions.DependencyInjection;

namespace Unayka.Core.Administration;

public static class AdministrationServiceExtensions
{
    public static IServiceCollection AddUnaykaAdministration(this IServiceCollection services)
    {
        services.AddScoped<IAdminDashboard, AdminDashboardService>();
        services.AddScoped<IAdminOrders, AdminOrdersService>();
        services.AddScoped<IAdminCatalog, AdminCatalogService>();
        services.AddScoped<IAdminCoupons, AdminCouponsService>();
        services.AddScoped<IAdminCustomers, AdminCustomersService>();
        services.AddScoped<IAdminPayments, AdminPaymentsService>();
        services.AddScoped<IAdminAudit, AdminAuditService>();
        return services;
    }
}
