using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShortLink.Api.Data;
using ShortLink.Api.Domain;

namespace ShortLink.Api.Services;

public class UrlService : IUrlService
{
    // 62^7 is ~3.5e12, so a single collision is already unlikely and three in a row on the
    // same request effectively cannot happen. The cap exists so a genuine bug (a generator
    // stuck on one value, say) fails loudly instead of spinning forever.
    private const int MaxInsertAttempts = 5;

    // Postgres "unique_violation". Any other DbUpdateException is a real fault and must not
    // be swallowed by the retry loop.
    private const string UniqueViolationSqlState = "23505";

    private readonly AppDbContext _db;
    private readonly IShortCodeGenerator _codeGenerator;

    public UrlService(AppDbContext db, IShortCodeGenerator codeGenerator)
    {
        _db = db;
        _codeGenerator = codeGenerator;
    }

    public async Task<ShortUrl?> ShortenAsync(string targetUrl)
    {
        if (!UrlValidator.TryNormalize(targetUrl, out var normalized))
        {
            return null;
        }

        for (var attempt = 1; attempt <= MaxInsertAttempts; attempt++)
        {
            var shortUrl = new ShortUrl
            {
                Code = _codeGenerator.Generate(),
                TargetUrl = normalized,
                CreatedAt = DateTime.UtcNow
            };

            _db.ShortUrls.Add(shortUrl);

            try
            {
                await _db.SaveChangesAsync();
                return shortUrl;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // The insert failed, but the entity is still tracked as Added. Leaving it
                // attached would make the next SaveChangesAsync retry this same doomed row
                // alongside the new one.
                _db.Entry(shortUrl).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException(
            $"Could not allocate a free short code in {MaxInsertAttempts} attempts.");
    }

    public Task<ShortUrl?> GetByCodeAsync(string code) =>
        _db.ShortUrls.AsNoTracking().FirstOrDefaultAsync(u => u.Code == code);

    public async Task<ShortUrl?> ResolveAndCountClickAsync(string code)
    {
        var shortUrl = await _db.ShortUrls.AsNoTracking().FirstOrDefaultAsync(u => u.Code == code);

        if (shortUrl is null)
        {
            return null;
        }

        // Incremented by the database rather than by loading, adding one, and saving. Two
        // simultaneous clicks would otherwise both read the same value and both write back
        // the same value+1, losing a click.
        await _db.ShortUrls
            .Where(u => u.Id == shortUrl.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ClickCount, u => u.ClickCount + 1));

        // The row above was read before the increment, so reflect it in what we hand back.
        shortUrl.ClickCount++;
        return shortUrl;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };
}
