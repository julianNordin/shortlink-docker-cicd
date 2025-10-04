using System.Collections.Concurrent;
using ShortLink.Api.Domain;

namespace ShortLink.Api.Services;

// Temporary store so the endpoints can be built and exercised before PostgreSQL exists.
// Replaced by an EF Core-backed implementation in Phase 04.
public class InMemoryUrlService : IUrlService
{
    private readonly ConcurrentDictionary<string, ShortUrl> _byCode = new();
    private readonly IShortCodeGenerator _codeGenerator;
    private readonly Lock _clickLock = new();
    private int _nextId;

    public InMemoryUrlService(IShortCodeGenerator codeGenerator)
    {
        _codeGenerator = codeGenerator;
    }

    public Task<ShortUrl?> ShortenAsync(string targetUrl)
    {
        if (!UrlValidator.TryNormalize(targetUrl, out var normalized))
        {
            return Task.FromResult<ShortUrl?>(null);
        }

        ShortUrl shortUrl;
        do
        {
            shortUrl = new ShortUrl
            {
                Id = Interlocked.Increment(ref _nextId),
                Code = _codeGenerator.Generate(),
                TargetUrl = normalized,
                CreatedAt = DateTime.UtcNow
            };
        }
        while (!_byCode.TryAdd(shortUrl.Code, shortUrl));

        return Task.FromResult<ShortUrl?>(shortUrl);
    }

    public Task<ShortUrl?> GetByCodeAsync(string code)
    {
        _byCode.TryGetValue(code, out var shortUrl);
        return Task.FromResult(shortUrl);
    }

    public Task<ShortUrl?> ResolveAndCountClickAsync(string code)
    {
        if (!_byCode.TryGetValue(code, out var shortUrl))
        {
            return Task.FromResult<ShortUrl?>(null);
        }

        // ClickCount is a property, so Interlocked can't take a ref to it.
        lock (_clickLock)
        {
            shortUrl.ClickCount++;
        }

        return Task.FromResult<ShortUrl?>(shortUrl);
    }
}
