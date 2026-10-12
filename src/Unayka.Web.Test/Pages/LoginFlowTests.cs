using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Unayka.Web.Test.Pages;

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
    public async Task Mocked_google_sign_in_creates_a_customer_session_outside_production()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/conta/entrar/externo/Google");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/conta", response.Headers.Location!.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("unayka_auth=", StringComparison.Ordinal));
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
            "/conta/entrar/equipa", Form(token, ("email", "admin@unayka.co.mz"), ("password", "unayka2026")));

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
