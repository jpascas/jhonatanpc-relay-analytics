using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Relay.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One SQL Server container per test run, plus one seeded API (database <see cref="SharedDatabase"/>) shared by the
/// read-only tests. Tests that need their own startup create a <see cref="RelayApiFactory"/> on another database.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string SharedDatabase = "relay_shared";

    // Same image as docker-compose.yml; Testcontainers maps a random host port, so the compose DB on 1433 is untouched.
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private RelayApiFactory? _api;

    public HttpClient Client { get; private set; } = null!;

    public string ConnectionString(string database) =>
        new SqlConnectionStringBuilder(_sqlServer.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();
        _api = new RelayApiFactory(ConnectionString(SharedDatabase));
        // Creating the client starts the app: Development migrates and seeds the shared database (D28).
        Client = _api.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }
        await _sqlServer.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "API with SQL Server";
}
