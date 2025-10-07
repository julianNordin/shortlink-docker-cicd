using Microsoft.EntityFrameworkCore;
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

app.Run();
