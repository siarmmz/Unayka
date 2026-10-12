using Unayka.Core.Configuration;
using Unayka.Web.Components;
using Unayka.Web.Endpoints;

DotNetEnv.Env.NoClobber().Load();

AppEnvConfig.Validate();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthEndpoint();
app.MapRazorComponents<App>();

app.Run();

public partial class Program;
