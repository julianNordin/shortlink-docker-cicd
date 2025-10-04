namespace ShortLink.Api.Domain;

public class ShortUrl
{
    public int Id { get; set; }

    // The generated Base62 code that appears in the short link, e.g. "aK3xQ0p".
    public string Code { get; set; } = string.Empty;

    // The absolute http/https URL the code redirects to.
    public string TargetUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public long ClickCount { get; set; }
}
