namespace ShortLink.Api.Services;

// Accepting whatever string arrives would turn the shortener into an open redirect for
// javascript: and data: URIs, so the scheme allow-list is the point of this class.
public static class UrlValidator
{
    public const int MaxUrlLength = 2048;

    public static bool TryNormalize(string? url, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();

        if (trimmed.Length > MaxUrlLength)
        {
            return false;
        }

        // UriKind.Absolute also rules out relative paths like "/admin", which would
        // otherwise redirect back into this app.
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        normalized = uri.ToString();
        return true;
    }

    public static bool IsValid(string? url) => TryNormalize(url, out _);
}
