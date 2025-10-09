using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Contracts;
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

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddScoped<IUrlService, UrlService>();

var app = builder.Build();

await DbInitializer.MigrateAsync(app.Services);

app.MapPost("/api/urls", async (ShortenRequest request, IUrlService urls, HttpRequest httpRequest) =>
{
    var shortUrl = await urls.ShortenAsync(request.Url ?? string.Empty);

    if (shortUrl is null)
    {
        return Results.BadRequest();
    }

    var response = ShortUrlResponse.From(shortUrl, httpRequest);

    // Location points at the stats resource rather than at the short link. 201 Location
    // means "where the thing you just created can be fetched", and fetching the short link
    // gives you a redirect to somewhere else entirely, not a representation of the record.
    return Results.Created($"/api/urls/{response.Code}", response);
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
