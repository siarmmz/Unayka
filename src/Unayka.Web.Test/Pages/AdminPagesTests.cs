using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Unayka.Web.Test.Pages;

[Collection(SharedWebServer.Name)]
public sealed partial class AdminPagesTests(UnaykaWebFactory factory)
{
    private const string LoginPath = "/conta/entrar";
    private const string TokenField = "__RequestVerificationToken";
    private const string Password = "unayka2026";

    private static readonly SemaphoreSlim SignInLock = new(1, 1);
    private static readonly Dictionary<string, HttpClient> SignedInClients = [];

    [Theory]
    [InlineData("/admin", "Painel")]
    [InlineData("/admin/pedidos", "Pedidos")]
    [InlineData("/admin/pedidos/UN-000001", "UN-000001")]
    [InlineData("/admin/produtos", "Produtos")]
    [InlineData("/admin/produtos/novo", "Novo produto")]
    [InlineData("/admin/categorias", "Categorias")]
    [InlineData("/admin/cupoes", "Cupões")]
    [InlineData("/admin/clientes", "Clientes")]
    [InlineData("/admin/pagamentos", "Pagamentos")]
    [InlineData("/admin/auditoria", "Auditoria")]
    public async Task Administrator_sees_every_admin_page(string path, string expectedText)
    {
        var client = await SignInAsync("admin@unayka.co.mz");

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(System.Net.WebUtility.HtmlEncode(expectedText), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cashier_can_open_orders_but_not_audit()
    {
        var client = await SignInAsync("caixa@unayka.co.mz");

        var orders = await client.GetAsync("/admin/pedidos");
        var audit = await client.GetAsync("/admin/auditoria");

        Assert.Equal(HttpStatusCode.OK, orders.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, audit.StatusCode);
    }

    [Fact]
    public async Task Anonymous_visitor_does_not_see_admin_content()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        await SignInLock.WaitAsync();
        try
        {
            if (!SignedInClients.TryGetValue(email, out var cached))
            {
                cached = await CreateSignedInClientAsync(email);
                SignedInClients[email] = cached;
            }

            return cached;
        }
        finally
        {
            SignInLock.Release();
        }
    }

    private async Task<HttpClient> CreateSignedInClientAsync(string email)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetStringAsync(LoginPath);
        var token = TokenRegex().Match(page).Groups[1].Value;
        var form = new FormUrlEncodedContent(
        [
            KeyValuePair.Create("email", email),
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
