using Microsoft.EntityFrameworkCore;
using Relay.Api.Data;
using Relay.Api.IntegrationTests.Infrastructure;

namespace Relay.Api.IntegrationTests;

/// <summary>D28 / S2: Development migrates and seeds once; Production never does.</summary>
[Collection(ApiCollection.Name)]
public class SeedingTests(ApiFixture fixture)
{
    [Fact] // G-03
    public async Task Development_start_seeds_the_whole_dataset()
    {
        var counts = await TestHelpers.CountRowsAsync(fixture.ConnectionString(ApiFixture.SharedDatabase));

        Assert.Equal((20, 12_626, 12_614), counts);
    }

    [Fact]
    public async Task Second_Development_start_does_not_seed_again()
    {
        var connectionString = fixture.ConnectionString(ApiFixture.SharedDatabase);

        await using var secondStart = new RelayApiFactory(connectionString);
        var health = await secondStart.CreateClient().GetAsync("/api/health");

        Assert.True(health.IsSuccessStatusCode);
        Assert.Equal((20, 12_626, 12_614), await TestHelpers.CountRowsAsync(connectionString));
    }

    [Fact]
    public async Task Production_start_does_not_seed()
    {
        var connectionString = fixture.ConnectionString("relay_production");
        // Outside Development the schema comes from migrations run separately (`dotnet ef database update`).
        await using (var db = new RelayDbContext(new DbContextOptionsBuilder<RelayDbContext>().UseSqlServer(connectionString).Options))
        {
            await db.Database.MigrateAsync();
        }

        await using var production = new RelayApiFactory(connectionString, environment: "Production");
        var health = await production.CreateClient().GetAsync("/api/health");

        Assert.True(health.IsSuccessStatusCode);
        Assert.Equal((0, 0, 0), await TestHelpers.CountRowsAsync(connectionString));
    }
}
