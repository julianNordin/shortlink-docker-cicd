using ShortLink.Api.Domain;

namespace ShortLink.Api.Services;

public interface IUrlService
{
    // Returns the stored ShortUrl, or null when targetUrl fails validation.
    Task<ShortUrl?> ShortenAsync(string targetUrl);

    Task<ShortUrl?> GetByCodeAsync(string code);

    // Resolves a code and records the click in one call.
    Task<ShortUrl?> ResolveAndCountClickAsync(string code);
}
