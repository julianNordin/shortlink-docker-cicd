using ShortLink.Api.Domain;

namespace ShortLink.Api.Contracts;

public record ShortUrlResponse(
    string Code,
    string ShortUrl,
    string TargetUrl,
    DateTime CreatedAt,
    long ClickCount)
{
    // The short link is built from the incoming request rather than from configuration, so
    // it stays correct whether the app is reached on localhost:5000, localhost:8080 in a
    // container, or some other host entirely.
    public static ShortUrlResponse From(ShortUrl url, HttpRequest request) =>
        new(url.Code,
            $"{request.Scheme}://{request.Host}/{url.Code}",
            url.TargetUrl,
            url.CreatedAt,
            url.ClickCount);
}
