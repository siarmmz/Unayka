using Unayka.Core.Accounts;
using Unayka.Core.Administration;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Configuration;
using Unayka.Core.Counter;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Orders;
using Unayka.Core.Site;
using Unayka.Web.Components;
using Unayka.Web.Endpoints;

DotNetEnv.Env.NoClobber().Load();

AppEnvConfig.Validate();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddUnaykaSecurity(builder.Environment.IsDevelopment());

// Feature services: one line per feature, keep this block sorted by feature. Do not edit the lines of other features.
builder.Services.AddUnaykaData();
builder.Services.AddUnaykaAccounts();
builder.Services.AddUnaykaCarts();
builder.Services.AddUnaykaCatalog();
builder.Services.AddUnaykaCounter();
builder.Services.AddUnaykaLocations();
builder.Services.AddUnaykaOrders();
builder.Services.AddUnaykaSite();
builder.Services.AddUnaykaAdministration();

var app = builder.Build();

await app.Services.InitializeUnaykaDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseUnaykaSecurity(app.Environment.IsDevelopment());
app.UseUnaykaSetupGate();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthEndpoint();

// Feature endpoints: at most one line per feature.
app.MapAccountEndpoints();
app.MapCheckoutEndpoints();
app.MapDownloadEndpoints();
app.MapLocationEndpoints();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
