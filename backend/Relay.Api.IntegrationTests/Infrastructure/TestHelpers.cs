using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;

namespace Relay.Api.IntegrationTests.Infrastructure;

public static class TestHelpers
{
    /// <summary>GETs a URL, asserts 200, and returns the body as JSON (the wire format, e.g. snake_case statuses).</summary>
    public static async Task<JsonNode> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"GET {url} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    public static JsonNode Location(this JsonNode body, string name) =>
        body["locations"]!.AsArray().Single(l => (string)l!["location"]! == name)!;

    /// <summary>Row counts straight from the database: accounts, events, and rows in the dedup view (D8).</summary>
    public static async Task<(int Accounts, int Events, int Dedup)> CountRowsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT (SELECT COUNT(*) FROM accounts), (SELECT COUNT(*) FROM activity_events), (SELECT COUNT(*) FROM activity_events_dedup)",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }
}
