using Microsoft.EntityFrameworkCore;

namespace ShortLink.Api.Data;

public static class DbInitializer
{
    // Applying migrations at startup is what keeps `docker compose up` a one-step story: the
    // api container comes up against an empty volume and builds its own schema. That holds
    // for a single instance, which is what this project runs. Several replicas starting at
    // once would race to apply the same migration, and migrations would need to move into a
    // separate step ahead of the rollout.
    public static async Task MigrateAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
