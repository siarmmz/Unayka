using System.Net;

namespace Unayka.Web.Test.Pages;

[Collection(SharedWebServer.Name)]
public sealed class StorefrontTests(UnaykaWebFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/loja")]
    [InlineData("/loja/todos?page=2&ordem=preco-asc")]
    [InlineData("/loja/todos")]
    [InlineData("/loja/chas")]
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
    public async Task Home_lists_featured_teas_and_cosmetics_rows()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains("Destaques", html, StringComparison.Ordinal);
        Assert.Contains("/loja/cosmeticos", html, StringComparison.Ordinal);
        Assert.Contains("/loja/chas", html, StringComparison.Ordinal);
        Assert.Contains("/produto/cappuccino-cremoso", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shop_search_echoes_the_term_and_filters_results()
    {
        var html = await _client.GetStringAsync("/loja/todos?q=cappuccino");

        Assert.Contains("value=\"cappuccino\"", html, StringComparison.Ordinal);
        Assert.Contains("/produto/cappuccino-cremoso", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/produto/espresso-da-casa", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shop_without_matches_shows_empty_state()
    {
        var html = await _client.GetStringAsync("/loja/todos?q=zzzzzz");

        Assert.Contains("Nada por aqui", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Product_page_posts_to_cart_endpoints_with_antiforgery()
    {
        var html = await _client.GetStringAsync("/produto/cappuccino-cremoso");

        Assert.Contains("action=\"/carrinho/adicionar\"", html, StringComparison.Ordinal);
        Assert.Contains("formaction=\"/carrinho/comprar-agora\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ReturnUrl\" value=\"/produto/cappuccino-cremoso?adicionado=cappuccino-cremoso\"", html, StringComparison.Ordinal);
        Assert.Contains("\"priceCurrency\":\"MZN\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Section_page_lists_tag_chips_and_filters_by_tag()
    {
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/loja/chas"));
        var filtered = await _client.GetStringAsync("/loja/chas?etiqueta=calmante");

        Assert.Contains("Calmante", html, StringComparison.Ordinal);
        Assert.Contains("etiqueta=calmante", html, StringComparison.Ordinal);
        Assert.Contains("Sem cafeína", html, StringComparison.Ordinal);
        Assert.Contains("/produto/", filtered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shop_and_header_loop_over_the_active_sections()
    {
        var shop = await _client.GetStringAsync("/loja");

        Assert.Contains("href=\"/loja/chas\"", shop, StringComparison.Ordinal);
        Assert.Contains("href=\"/loja/cosmeticos\"", shop, StringComparison.Ordinal);
        Assert.Contains("href=\"/loja/livros\"", shop, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Header_shows_the_cart_label_and_footer_lists_help_and_contacts()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains(">Carrinho<", html, StringComparison.Ordinal);
        Assert.Contains("Como comprar", html, StringComparison.Ordinal);
        Assert.Contains("Levantamento", html, StringComparison.Ordinal);
        Assert.Contains("Pagamentos", html, StringComparison.Ordinal);
        Assert.Contains("<unayka-collapse", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Product_page_offers_share_links_and_location_availability()
    {
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/produto/cappuccino-cremoso"));

        Assert.Contains("Partilhar", html, StringComparison.Ordinal);
        Assert.Contains("https://wa.me/?text=", html, StringComparison.Ordinal);
        Assert.Contains("facebook.com/sharer", html, StringComparison.Ordinal);
        Assert.Contains("Copiar link", html, StringComparison.Ordinal);
        Assert.Contains("Disponível em", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Digital_product_page_explains_the_download()
    {
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/produto/e-book-receitas-com-cha"));

        Assert.Contains("Produto digital, descarregue após o pagamento", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cards_have_add_to_cart_label_share_and_fade_images()
    {
        var html = await _client.GetStringAsync("/loja/todos");

        Assert.Contains("aria-label=\"Adicionar ao carrinho\"", html, StringComparison.Ordinal);
        Assert.Contains("<unayka-fade", html, StringComparison.Ordinal);
        Assert.Contains("<unayka-share", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Added_flag_shows_the_cart_confirmation()
    {
        var html = await _client.GetStringAsync("/loja/todos?adicionado=cappuccino-cremoso");

        Assert.Contains("foi adicionado ao carrinho", html, StringComparison.Ordinal);
        Assert.Contains("Ver carrinho", html, StringComparison.Ordinal);
        Assert.Contains("Continuar a comprar", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_in_page_shows_brand_buttons_with_brand_tokens()
    {
        var html = await _client.GetStringAsync("/conta/entrar");

        Assert.Contains("Continuar com Google", html, StringComparison.Ordinal);
        Assert.Contains("bg-brand-google", html, StringComparison.Ordinal);
        Assert.Contains("Continuar com Facebook", html, StringComparison.Ordinal);
        Assert.Contains("bg-brand-facebook", html, StringComparison.Ordinal);
    }
}
