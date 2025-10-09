namespace ShortLink.Api.Contracts;

// Url is nullable so a missing or null value reaches UrlValidator and comes back as a
// 400 ProblemDetails, rather than the JSON binder rejecting it with a shape the rest of
// the API never produces.
public record ShortenRequest(string? Url);
