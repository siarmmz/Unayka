using System.Net;

namespace Unayka.Web.Test.Pages;

[Collection(SharedWebServer.Name)]
public sealed class StorefrontTests(UnaykaWebFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/loja")]
    [InlineData("/loja?page=2&ordem=preco-asc")]
    [InlineData("/loja/cafes")]
    [InlineData("/loja/cosmeticos")]
    [InlineData("/loja/categoria/cafes-quentes")]
    [InlineData("/produto/cappuccino-cremoso")]
    public async Task Storefront_page_responds_with_seo_and_structured_data(string path)
    {
        var response = await _client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("rel=\"canonical\"", html, StringComparison.Ordinal);
        Assert.Contains("application/ld&#x2B;json", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/loja/categoria/does-not-exist")]
    [InlineData("/produto/does-not-exist")]
    public async Task Unknown_category_or_product_is_not_found(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Home_lists_featured_products_and_categories()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains("Destaques", html, StringComparison.Ordinal);
        Assert.Contains("Mais vendidos", html, StringComparison.Ordinal);
        Assert.Contains("/loja/categoria/cafes-quentes", html, StringComparison.Ordinal);
        Assert.Contains("/produto/cappuccino-cremoso", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shop_search_echoes_the_term_and_filters_results()
    {
        var html = await _client.GetStringAsync("/loja?q=cappuccino");

        Assert.Contains("value=\"cappuccino\"", html, StringComparison.Ordinal);
        Assert.Contains("/produto/cappuccino-cremoso", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/produto/espresso-da-casa", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shop_without_matches_shows_empty_state()
    {
        var html = await _client.GetStringAsync("/loja?q=zzzzzz");

        Assert.Contains("Nada por aqui", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Product_page_posts_to_cart_endpoints_with_antiforgery()
    {
        var html = await _client.GetStringAsync("/produto/cappuccino-cremoso");

        Assert.Contains("action=\"/carrinho/adicionar\"", html, StringComparison.Ordinal);
        Assert.Contains("formaction=\"/carrinho/comprar-agora\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ReturnUrl\" value=\"/produto/cappuccino-cremoso\"", html, StringComparison.Ordinal);
        Assert.Contains("\"priceCurrency\":\"MZN\"", html, StringComparison.Ordinal);
    }
}
