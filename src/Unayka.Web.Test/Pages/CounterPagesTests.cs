using System.Net;
using System.Text.RegularExpressions;

namespace Unayka.Web.Test.Pages;

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
            KeyValuePair.Create("password", "unayka2026"),
            KeyValuePair.Create(TokenField, token),
        ]);
        await client.PostAsync("/conta/entrar/equipa", form);
        return client;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
