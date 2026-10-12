using System.Net;
using System.Text.RegularExpressions;

namespace Unayka.Web.Test.Pages;

[Collection(SharedWebServer.Name)]
public sealed partial class PublicPagesTests(UnaykaWebFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/services")]
    [InlineData("/about")]
    [InlineData("/location")]
    [InlineData("/contact")]
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
        Assert.Contains("images.unsplash.com", string.Join(" ", response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<title>[^<]*Unayka[^<]*</title>")]
    private static partial Regex TitleRegex();
}
