using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Domain;
using ShortLink.Api.Services;

namespace ShortLink.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var shortUrl = modelBuilder.Entity<ShortUrl>();

        // Lengths come from the constants that already govern generation and validation, so
        // the column can't silently disagree with the code that writes to it.
        shortUrl.Property(u => u.Code)
            .HasMaxLength(ShortCodeGenerator.CodeLength)
            .IsRequired();

        // This unique index is what makes the generator's retry-on-collision approach
        // actually correct. Without it, two requests that happened to draw the same code
        // would both insert happily and one short link would quietly shadow the other.
        shortUrl.HasIndex(u => u.Code).IsUnique();

        shortUrl.Property(u => u.TargetUrl)
            .HasMaxLength(UrlValidator.MaxUrlLength)
            .IsRequired();

        // CreatedAt maps to `timestamp with time zone`, which Npgsql only accepts from a
        // DateTime whose Kind is Utc — hence DateTime.UtcNow at every write site.
        shortUrl.Property(u => u.CreatedAt).IsRequired();
    }
}
