using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Unayka.Web.Test.Pages;

[Collection(SharedWebServer.Name)]
public sealed partial class CheckoutFlowTests(UnaykaWebFactory factory)
{
    private const string TokenField = "__RequestVerificationToken";
    private const string TokenSourcePath = "/conta/entrar";
    private const string Phone = "841234567";

    [Theory]
    [InlineData("/carrinho")]
    [InlineData("/checkout")]
    public async Task Cart_and_checkout_pages_respond_with_ok(string path)
    {
        var response = await CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Empty_cart_page_links_to_the_shop()
    {
        var html = await CreateClient().GetStringAsync("/carrinho");

        Assert.Contains("href=\"/loja\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adding_to_the_cart_without_antiforgery_is_rejected()
    {
        var response = await CreateClient().PostAsync(
            "/carrinho/adicionar", new FormUrlEncodedContent(new Dictionary<string, string> { ["productId"] = "1" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Full_purchase_flow_places_an_order_visible_only_to_the_buyer()
    {
        var client = CreateClient();
        var token = await ReadTokenAsync(client);

        var add = await client.PostAsync("/carrinho/adicionar", Form(token, ("productId", "1"), ("quantity", "2"), ("returnUrl", "/carrinho")));
        var cartHtml = await client.GetStringAsync("/carrinho");
        var coupon = await client.PostAsync("/carrinho/cupao", Form(token, ("code", "bemvindo10")));
        var couponHtml = await client.GetStringAsync(coupon.Headers.Location!.ToString());
        var checkoutHtml = await client.GetStringAsync("/checkout");
        var placed = await client.PostAsync("/checkout/confirmar", Form(
            token, ("name", "Ana Silva"), ("phone", Phone), ("fulfillment", "Delivery"), ("address", "Polana Cimento"), ("method", "MPesa"), ("walletPhone", Phone)));
        var orderPath = placed.Headers.Location!.ToString();
        var orderResponse = await client.GetAsync(orderPath);
        var strangerResponse = await CreateClient().GetAsync(orderPath);

        Assert.Equal("/carrinho", add.Headers.Location!.OriginalString);
        Assert.Contains("Continuar para pagamento", cartHtml, StringComparison.Ordinal);
        Assert.Contains("Cup&#227;o aplicado", couponHtml, StringComparison.Ordinal);
        Assert.Contains("Confirmar e pagar", checkoutHtml, StringComparison.Ordinal);
        Assert.Matches(@"^/pedido/UN-\d{6}$", orderPath);
        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerResponse.StatusCode);
    }

    [Fact]
    public async Task Checkout_with_missing_name_returns_to_the_form_with_an_error()
    {
        var client = CreateClient();
        var token = await ReadTokenAsync(client);
        await client.PostAsync("/carrinho/adicionar", Form(token, ("productId", "1")));

        var response = await client.PostAsync("/checkout/confirmar", Form(token, ("phone", Phone), ("walletPhone", Phone)));
        var html = await client.GetStringAsync(response.Headers.Location!.ToString());

        Assert.StartsWith("/checkout?erro=InvalidName", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains("Indique o seu nome", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Account_page_requires_sign_in()
    {
        var response = await CreateClient().GetAsync("/conta");

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Os seus pedidos", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static FormUrlEncodedContent Form(string token, params (string Key, string Value)[] fields) =>
        new(fields.Select(field => KeyValuePair.Create(field.Key, field.Value)).Append(KeyValuePair.Create(TokenField, token)));

    private static async Task<string> ReadTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync(TokenSourcePath);
        return TokenRegex().Match(html).Groups[1].Value;
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
