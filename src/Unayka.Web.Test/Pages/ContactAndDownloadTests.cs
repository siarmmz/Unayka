using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Unayka.Core.Locations;
using Unayka.Web.Components.Locations;

namespace Unayka.Web.Test.Pages;

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
