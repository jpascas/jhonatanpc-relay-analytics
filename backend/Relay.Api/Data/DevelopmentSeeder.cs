using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Relay.Api.Data;

/// <summary>
/// D28: in Development only, apply migrations, then load seed.sql (used as-is) when <c>accounts</c> is empty.
/// The empty-accounts guard makes restarts a no-op: the seed inserts fixed ids, so a second run would fail.
/// </summary>
public static class DevelopmentSeeder
{
    public static async Task RunAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RelayDbContext>>();

        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Accounts.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed skipped: accounts table already has rows.");
            return;
        }

        var seedPath = options.ResolvePath(app.Environment.ContentRootPath);
        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException(
                $"Seed file not found at '{seedPath}'. Set '{SeedOptions.SectionName}:Path'.", seedPath);
        }

        var sql = await File.ReadAllTextAsync(seedPath, cancellationToken);

        // One batch of ~12.6k INSERTs; the default 30 s command timeout is too tight on a cold container.
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Seeded database from {SeedPath}.", seedPath);
    }
}
