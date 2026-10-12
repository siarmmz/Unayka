using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Unayka.Core.Locations;
using Unayka.Web.Components.Locations;

namespace Unayka.Web.Test;

[Collection(SharedWebServer.Name)]
public sealed partial class PublicPagesTests(UnaykaWebFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/about")]
    [InlineData("/contacto")]
    [InlineData("/conta/entrar")]
    public async Task Public_page_responds_with_seo_title_and_description(string path)
    {
        var response = await _client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(TitleRegex(), html);
        Assert.True(html.Contains("name=\"description\"", StringComparison.Ordinal), html);
        Assert.True(html.Contains("rel=\"canonical\"", StringComparison.Ordinal), html);
        Assert.True(html.Contains("application/ld&#x2B;json", StringComparison.Ordinal), html);
    }

    [Fact]
    public async Task Health_returns_ok_with_active_database_and_storage()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"database\":\"sqlite\"", body, StringComparison.Ordinal);
        Assert.Contains("\"storage\":\"local\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_route_shows_not_found_page()
    {
        var response = await _client.GetAsync("/does-not-exist");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Matches(TitleRegex(), html);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        var policy = string.Join(" ", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("img-src 'self' data: https:", policy, StringComparison.Ordinal);
        Assert.Contains("https://cdn.jsdelivr.net", policy, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<title>[^<]*Unayka[^<]*</title>")]
    private static partial Regex TitleRegex();
}

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
    [InlineData("/loja/categoria/cafe")]
    [InlineData("/produto/cappuccino")]
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
        Assert.Contains("/produto/cappuccino", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shop_search_echoes_the_term_and_filters_results()
    {
        var html = await _client.GetStringAsync("/loja/todos?q=cappuccino");

        Assert.Contains("value=\"cappuccino\"", html, StringComparison.Ordinal);
        Assert.Contains("/produto/cappuccino", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/produto/espresso", html, StringComparison.Ordinal);
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
        var html = await _client.GetStringAsync("/produto/cappuccino");

        Assert.Contains("action=\"/carrinho/adicionar\"", html, StringComparison.Ordinal);
        Assert.Contains("formaction=\"/carrinho/comprar-agora\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ReturnUrl\" value=\"/produto/cappuccino?adicionado=cappuccino\"", html, StringComparison.Ordinal);
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
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/produto/cappuccino"));

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
        var html = await _client.GetStringAsync("/loja/todos?adicionado=cappuccino");

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

[Collection(SharedWebServer.Name)]
public sealed class ContactAndDownloadTests(UnaykaWebFactory factory)
{
    [Fact]
    public async Task Contact_page_lists_every_location_with_links_and_a_lazy_map()
    {
        var html = await CreateClient().GetStringAsync("/contacto");

        Assert.Contains("Unayka Vilanculos", html, StringComparison.Ordinal);
        Assert.Contains("Unayka Maputo, Polana", html, StringComparison.Ordinal);
        Assert.Matches(@"href=""tel:(\+|&#x2B;)258840000101""", html);
        Assert.Contains("https://wa.me/258820000202", html, StringComparison.Ordinal);
        Assert.Contains("mailto:maputo@unayka.co.mz", html, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", html, StringComparison.Ordinal);
        Assert.Contains("https://www.openstreetmap.org/export/embed.html", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/contact")]
    [InlineData("/location")]
    [InlineData("/localizacao")]
    public async Task Old_contact_and_location_routes_redirect_to_the_contact_page(string path)
    {
        var response = await CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/contacto", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task The_content_security_policy_allows_only_the_map_provider_frames()
    {
        var response = await CreateClient().GetAsync("/");

        Assert.Contains("frame-src https://www.openstreetmap.org", string.Join(" ", response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_anonymous_visitor_cannot_download()
    {
        var response = await CreateClient().GetAsync("/conta/descargas/1");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("/conta/entrar", response.Headers.Location?.AbsolutePath ?? response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Contact_links_use_international_numbers_and_only_trusted_map_hosts()
    {
        var phone = ContactLinks.For(new LocationContactSummary(ContactKind.Phone, "Chamadas", "+258 84 000 0101"));
        var whatsApp = ContactLinks.For(new LocationContactSummary(ContactKind.WhatsApp, "WhatsApp", "84 000 0101"));
        var email = ContactLinks.For(new LocationContactSummary(ContactKind.Email, "Email", " loja@unayka.co.mz "));

        Assert.Equal("tel:+258840000101", phone.Url);
        Assert.Equal("https://wa.me/258840000101", whatsApp.Url);
        Assert.Equal("mailto:loja@unayka.co.mz", email.Url);
        Assert.NotNull(ContactLinks.SafeMapUrl("https://www.openstreetmap.org/export/embed.html?bbox=1"));
        Assert.Null(ContactLinks.SafeMapUrl("https://evil.example/map"));
        Assert.Null(ContactLinks.SafeMapUrl(null));
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

[Collection(SharedWebServer.Name)]
public sealed partial class LoginFlowTests(UnaykaWebFactory factory)
{
    private const string LoginPath = "/conta/entrar";
    private const string TokenField = "__RequestVerificationToken";

    [Fact]
    public async Task Login_page_offers_google_facebook_and_staff_sign_in()
    {
        var client = CreateClient();

        var html = await client.GetStringAsync(LoginPath);

        Assert.Contains("/conta/entrar/externo/Google", html, StringComparison.Ordinal);
        Assert.Contains("/conta/entrar/externo/Facebook", html, StringComparison.Ordinal);
        Assert.Contains("/conta/entrar/equipa", html, StringComparison.Ordinal);
        Assert.Contains(TokenField, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var client = CreateClient();

        var response = await client.PostAsync("/conta/entrar/equipa", new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "admin@unayka.co.mz" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Google_sign_in_without_keys_reports_it_is_unavailable()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/conta/entrar/externo/Google");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/conta/entrar?error=ExternalUnavailable", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Unknown_provider_returns_not_found()
    {
        var response = await CreateClient().GetAsync("/conta/entrar/externo/Telegram");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Staff_login_redirects_to_administration_and_sets_the_auth_cookie()
    {
        var client = CreateClient();
        var token = await ReadTokenAsync(client);

        var response = await client.PostAsync(
            "/conta/entrar/equipa", Form(token, ("email", "admin@unayka.co.mz"), ("password", UnaykaWebFactory.StaffPassword)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location!.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("unayka_auth=", StringComparison.Ordinal) && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Wrong_staff_password_returns_to_the_login_page_with_an_error()
    {
        var client = CreateClient();
        var token = await ReadTokenAsync(client);

        var response = await client.PostAsync(
            "/conta/entrar/equipa", Form(token, ("email", "admin@unayka.co.mz"), ("password", "wrong")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=InvalidCredentials", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    private static FormUrlEncodedContent Form(string token, params (string Key, string Value)[] fields) =>
        new(fields.Select(field => KeyValuePair.Create(field.Key, field.Value)).Append(KeyValuePair.Create(TokenField, token)));

    private static async Task<string> ReadTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync(LoginPath);
        return TokenRegex().Match(html).Groups[1].Value;
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

}
