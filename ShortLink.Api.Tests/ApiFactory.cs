using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace ShortLink.Api.Tests;

// Boots the real application against a throwaway PostgreSQL container. No EF in-memory
// provider: the behaviour worth testing here is the unique index on Code and the atomic
// ClickCount update, and neither of those exists outside a real database.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        // Same image as docker-compose.yml, so the tests exercise the version that runs.
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Deliberately an environment variable rather than ConfigureAppConfiguration.
        // Program.cs reads builder.Configuration *before* builder.Build(), and a factory's
        // ConfigureAppConfiguration callbacks do not run until Build() - so they arrive too
        // late to be seen, and the app silently keeps the connection string from
        // appsettings.Development.json. That points at localhost:5432, which meant these
        // tests quietly ran against the developer's Compose database whenever one happened
        // to be up, and would have failed on the first CI run where none is.
        // WebApplication.CreateBuilder reads environment variables as it is created, which
        // is early enough. The host is built lazily on the first CreateClient(), well after
        // this runs.
        Environment.SetEnvironmentVariable(
            ConnectionStringVariable, _postgres.GetConnectionString());
    }

    // Explicit implementation: WebApplicationFactory already has a DisposeAsync returning
    // ValueTask, and xUnit's IAsyncLifetime wants one returning Task.
    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable(ConnectionStringVariable, null);
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
