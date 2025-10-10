using ShortLink.Api.Services;

namespace ShortLink.Api.Tests;

public class UrlValidationTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/a/path?query=1#fragment")]
    [InlineData("https://user:pass@example.com:8443/path")]
    public void Accepts_absolute_http_and_https_urls(string url)
    {
        Assert.True(UrlValidator.IsValid(url));
    }

    [Theory]
    // The whole reason the allow-list exists: without it these would make the shortener a
    // redirector for script and inline payloads.
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/file.txt")]
    // Relative URLs would redirect back into this app rather than out of it.
    [InlineData("/admin")]
    [InlineData("//example.com")]
    [InlineData("example.com")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_everything_else(string? url)
    {
        Assert.False(UrlValidator.IsValid(url));
    }

    [Fact]
    public void Rejects_urls_longer_than_the_maximum()
    {
        var tooLong = "https://example.com/" + new string('a', UrlValidator.MaxUrlLength);

        Assert.False(UrlValidator.IsValid(tooLong));
    }

    [Fact]
    public void Normalizes_surrounding_whitespace()
    {
        Assert.True(UrlValidator.TryNormalize("  https://example.com/x  ", out var normalized));
        Assert.Equal("https://example.com/x", normalized);
    }
}
