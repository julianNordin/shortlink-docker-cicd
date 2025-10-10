using System.Net;
using System.Net.Http.Json;
using ShortLink.Api.Contracts;

namespace ShortLink.Api.Tests;

public class EndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EndpointTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // Redirects must not be followed, or the test would chase the real target URL out onto
    // the internet instead of inspecting the 302 the app produced.
    private HttpClient CreateClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static async Task<ShortUrlResponse> ShortenAsync(HttpClient client, string url)
    {
        var response = await client.PostAsJsonAsync("/api/urls", new ShortenRequest(url));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShortUrlResponse>())!;
    }

    [Fact]
    public async Task Shorten_returns_201_with_a_location_header()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/urls",
            new ShortenRequest("https://example.com/a/page"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ShortUrlResponse>();
        Assert.NotNull(created);
        Assert.Equal("https://example.com/a/page", created.TargetUrl);
        Assert.Equal(0, created.ClickCount);
        Assert.Equal($"/api/urls/{created.Code}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Shorten_rejects_a_non_http_url_with_problem_details()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/urls",
            new ShortenRequest("javascript:alert(1)"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Redirect_sends_302_to_the_target()
    {
        var client = CreateClient();
        var created = await ShortenAsync(client, "https://example.com/destination");

        var response = await client.GetAsync($"/{created.Code}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://example.com/destination", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Redirect_increments_the_click_count()
    {
        var client = CreateClient();
        var created = await ShortenAsync(client, "https://example.com/counted");

        for (var i = 0; i < 3; i++)
        {
            await client.GetAsync($"/{created.Code}");
        }

        var stats = await client.GetFromJsonAsync<ShortUrlResponse>($"/api/urls/{created.Code}");

        Assert.NotNull(stats);
        Assert.Equal(3, stats.ClickCount);
    }

    [Fact]
    public async Task Reading_stats_does_not_count_as_a_click()
    {
        var client = CreateClient();
        var created = await ShortenAsync(client, "https://example.com/uncounted");

        await client.GetAsync($"/api/urls/{created.Code}");
        var stats = await client.GetFromJsonAsync<ShortUrlResponse>($"/api/urls/{created.Code}");

        Assert.NotNull(stats);
        Assert.Equal(0, stats.ClickCount);
    }

    [Fact]
    public async Task Unknown_code_redirect_returns_404()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/zzzzzzz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_code_stats_returns_404()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/urls/zzzzzzz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Each_shorten_gets_its_own_code()
    {
        var client = CreateClient();

        var first = await ShortenAsync(client, "https://example.com/one");
        var second = await ShortenAsync(client, "https://example.com/two");

        Assert.NotEqual(first.Code, second.Code);
    }
}
