using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Unayka.Web.Test;

[Collection(SharedWebServer.Name)]
public sealed partial class AdminCatalogPagesTests(UnaykaWebFactory factory)
{
    private const string LoginPath = "/conta/entrar";
    private const string TokenField = "__RequestVerificationToken";
    private const string Password = UnaykaWebFactory.StaffPassword;
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
    [InlineData("/admin/seccoes/1", "Categorias")]
    [InlineData("/admin/seccoes/1", "Etiquetas")]
    [InlineData("/admin/site", "Meios de pagamento")]
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
    [InlineData("/admin/site")]
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

[Collection(SharedWebServer.Name)]
public sealed partial class AdminPagesTests(UnaykaWebFactory factory)
{
    private const string LoginPath = "/conta/entrar";
    private const string TokenField = "__RequestVerificationToken";
    private const string Password = UnaykaWebFactory.StaffPassword;

    private static readonly SemaphoreSlim SignInLock = new(1, 1);
    private static readonly Dictionary<string, HttpClient> SignedInClients = [];

    [Theory]
    [InlineData("/admin", "Painel")]
    [InlineData("/admin/pedidos", "Pedidos")]
    [InlineData("/admin/pedidos/UN-000001", "UN-000001")]
    [InlineData("/admin/produtos", "Produtos")]
    [InlineData("/admin/produtos/novo", "Novo produto")]
    [InlineData("/admin/site", "Site")]
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

[Collection(SharedWebServer.Name)]
public sealed partial class CounterPagesTests(UnaykaWebFactory factory)
{
    private const string TokenField = "__RequestVerificationToken";

    [Theory]
    [InlineData("/balcao", "Abra o caixa para vender")]
    [InlineData("/balcao/caixa", "Abrir o caixa")]
    [InlineData("/balcao/preparacao", "Novo / Pago")]
    public async Task Staff_sees_counter_pages(string path, string marker)
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(marker, html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/balcao")]
    [InlineData("/balcao/caixa")]
    [InlineData("/balcao/preparacao")]
    public async Task Anonymous_visitors_do_not_see_counter_pages(string path)
    {
        var client = factory.CreateClient();

        var html = await client.GetStringAsync(path);

        Assert.DoesNotContain("Venda ao balc", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Novo / Pago", html, StringComparison.Ordinal);
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = factory.CreateClient();
        var loginHtml = await client.GetStringAsync("/conta/entrar");
        var token = TokenRegex().Match(loginHtml).Groups[1].Value;
        var form = new FormUrlEncodedContent(
        [
            KeyValuePair.Create("email", "caixa@unayka.co.mz"),
            KeyValuePair.Create("password", UnaykaWebFactory.StaffPassword),
            KeyValuePair.Create(TokenField, token),
        ]);
        await client.PostAsync("/conta/entrar/equipa", form);
        return client;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
