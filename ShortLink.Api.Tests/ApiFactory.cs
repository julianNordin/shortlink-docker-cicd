using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace ShortLink.Api.Tests;

// Boots the real application against a throwaway PostgreSQL container. No EF in-memory
// provider: the behaviour worth testing here is the unique index on Code and the atomic
// ClickCount update, and neither of those exists outside a real database.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        // Same image as docker-compose.yml, so the tests exercise the version that runs.
        .WithImage("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Added after the defaults, so this wins over appsettings.Development.json and
            // the tests can never accidentally run against the developer's own database.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString()
            });
        });
    }

    // Starts before any test touches CreateClient(), which is what builds the host — so the
    // container's connection string is available by the time configuration is read.
    // The app applies its own migrations on startup, so the schema arrives with it.
    public Task InitializeAsync() => _postgres.StartAsync();

    // Explicit implementation: WebApplicationFactory already has a DisposeAsync returning
    // ValueTask, and xUnit's IAsyncLifetime wants one returning Task.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
