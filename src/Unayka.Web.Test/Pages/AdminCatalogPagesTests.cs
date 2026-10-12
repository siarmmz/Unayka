using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Unayka.Web.Test.Pages;

[Collection(SharedWebServer.Name)]
public sealed partial class AdminCatalogPagesTests(UnaykaWebFactory factory)
{
    private const string LoginPath = "/conta/entrar";
    private const string TokenField = "__RequestVerificationToken";
    private const string Password = "unayka2026";
    private const string AdministratorEmail = "admin@unayka.co.mz";

    [Theory]
    [InlineData("/admin/produtos", "Todos")]
    [InlineData("/admin/produtos/novos", "Novos")]
    [InlineData("/admin/produtos/destaques", "Destaques")]
    [InlineData("/admin/produtos/inativos", "Inativos")]
    [InlineData("/admin/produtos/arquivados", "Arquivados")]
    [InlineData("/admin/produtos/1", "Editar produto")]
    [InlineData("/admin/produtos/novo", "Novo produto")]
    [InlineData("/admin/seccoes", "Secções")]
    [InlineData("/admin/categorias", "Categorias")]
    [InlineData("/admin/etiquetas", "Etiquetas")]
    [InlineData("/admin/lojas", "Lojas")]
    public async Task Administrator_sees_the_catalog_and_location_pages(string path, string expectedText)
    {
        var client = await SignInAsync();

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(WebUtility.HtmlEncode(expectedText), html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/admin/seccoes")]
    [InlineData("/admin/etiquetas")]
    [InlineData("/admin/lojas")]
    public async Task Anonymous_visitor_cannot_open_catalog_administration(string path)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> SignInAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetStringAsync(LoginPath);
        var token = TokenRegex().Match(page).Groups[1].Value;
        var form = new FormUrlEncodedContent(
        [
            KeyValuePair.Create("email", AdministratorEmail),
            KeyValuePair.Create("password", Password),
            KeyValuePair.Create(TokenField, token),
        ]);
        var response = await client.PostAsync("/conta/entrar/equipa", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
