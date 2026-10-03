using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Relay.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in-process against one database in the test SQL Server container. In Development the app
/// migrates and seeds itself at startup (D28), so tests also cover that path.
/// </summary>
public sealed class RelayApiFactory(
    string connectionString,
    string environment = "Development",
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        // Added last, so these win over appsettings.{Environment}.json (e.g. its localhost:1433 connection string).
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Relay"] = connectionString,
                ["Seed:Path"] = RepoPaths.SeedSql,
            }.Concat(settings ?? new Dictionary<string, string?>())));
    }
}
