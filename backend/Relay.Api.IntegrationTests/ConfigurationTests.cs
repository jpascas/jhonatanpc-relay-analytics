using Relay.Api.IntegrationTests.Infrastructure;

namespace Relay.Api.IntegrationTests;

/// <summary>D1/D2/D23: configuration changes behaviour at startup, on the real host.</summary>
[Collection(ApiCollection.Name)]
public class ConfigurationTests(ApiFixture fixture)
{
    [Fact] // D23 / S4 "Done when"
    public async Task Invalid_threshold_stops_startup_naming_the_setting()
    {
        await using var factory = new RelayApiFactory(
            fixture.ConnectionString(ApiFixture.SharedDatabase),
            settings: new Dictionary<string, string?> { ["StatusRules:RelativeGap"] = "1.5" });

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("StatusRules:RelativeGap", ex.ToString());
    }

    [Fact] // D1/D2: 2026-04-01 00:00 UTC is Tue 2026-03-31 19:00 in Chicago, so the last complete week is 2026-03-23
    public async Task Clock_setting_moves_the_default_week()
    {
        await using var factory = new RelayApiFactory(
            fixture.ConnectionString(ApiFixture.SharedDatabase),
            settings: new Dictionary<string, string?> { ["Clock:NowUtc"] = "2026-04-01T00:00:00Z" });

        var body = await factory.CreateClient().GetJsonAsync("/api/accounts/1/weekly-status");

        Assert.Equal("2026-03-23", (string)body["reportedWeek"]!["localStart"]!);
        Assert.Equal("2026-03-23", (string)body["availableWeeks"]!["latest"]!);
    }
}
