using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Contracts;
using ShortLink.Api.Domain;
using ShortLink.Api.Data;
using ShortLink.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Only lives in appsettings.Development.json; in a container Compose supplies it as the
// ConnectionStrings__Default environment variable. Failing loudly here beats letting EF
// throw something far less obvious on the first query.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "No 'Default' connection string. Set ConnectionStrings__Default, or run with the " +
        "Development environment and start the database with `docker compose up -d db`.");

// Makes unhandled failures come back as RFC 9457 ProblemDetails too, so a caller never has
// to parse one error shape for expected problems and an empty 500 for everything else.
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Probes the database rather than just answering 200. An API that cannot reach Postgres
// can serve nothing useful, so reporting itself healthy would make the check a liability -
// an orchestrator would keep routing traffic to an instance that fails every request.
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

builder.Services.AddSingleton<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddScoped<IUrlService, UrlService>();

var app = builder.Build();

app.UseExceptionHandler();

await DbInitializer.MigrateAsync(app.Services);

app.MapHealthChecks("/health");

app.MapPost("/api/urls", async (ShortenRequest request, IUrlService urls, HttpRequest httpRequest) =>
{
    ShortUrl? shortUrl;

    try
    {
        shortUrl = await urls.ShortenAsync(request.Url ?? string.Empty);
    }
    catch (ShortCodeExhaustedException)
    {
        // 503 rather than 500: nothing is broken, the generator just lost several draws in
        // a row, and retrying the same request is a reasonable thing for a caller to do.
        return Results.Problem(
            title: "Could not allocate a short code",
            detail: "The service could not find an unused short code. Please retry.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (shortUrl is null)
    {
        return Results.Problem(
            title: "Invalid URL",
            detail: $"'url' must be an absolute http or https URL of at most {UrlValidator.MaxUrlLength} characters.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var response = ShortUrlResponse.From(shortUrl, httpRequest);

    // Location points at the stats resource rather than at the short link. 201 Location
    // means "where the thing you just created can be fetched", and fetching the short link
    // gives you a redirect to somewhere else entirely, not a representation of the record.
    return Results.Created($"/api/urls/{response.Code}", response);
});

app.MapGet("/api/urls/{code}", async (string code, IUrlService urls, HttpRequest httpRequest) =>
{
    var shortUrl = await urls.GetByCodeAsync(code);

    // Deliberately does not count as a click: this is the record about the link, not a use
    // of it, and inflating the counter from the stats page would make the number meaningless.
    return shortUrl is null
        ? Results.Problem(
            title: "Unknown short code",
            detail: $"No short link exists for code '{code}'.",
            statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ShortUrlResponse.From(shortUrl, httpRequest));
});

app.MapGet("/{code}", async (string code, IUrlService urls) =>
{
    var shortUrl = await urls.ResolveAndCountClickAsync(code);

    // 302, not 301. A permanent redirect is cached by the browser, so every click after the
    // first would never reach this app and the counter would simply stop moving.
    return shortUrl is null
        ? Results.NotFound()
        : Results.Redirect(shortUrl.TargetUrl);
});

app.Run();

// Top-level statements compile into an internal Program class, which WebApplicationFactory
// cannot reach. Declaring it public here is what lets the test project spin the real app up.
public partial class Program;
