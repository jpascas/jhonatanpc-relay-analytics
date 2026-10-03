using System.Net;
using System.Text.Json.Nodes;
using Relay.Api.IntegrationTests.Infrastructure;

namespace Relay.Api.IntegrationTests;

/// <summary>
/// GET /api/accounts/{id}/weekly-status end to end (routing, SQL over the dedup view, rules, JSON) against the
/// seeded data. Expected values are the PLAN.md §6 golden values and the §5 edge cases.
/// </summary>
[Collection(ApiCollection.Name)]
public class WeeklyStatusTests(ApiFixture fixture)
{
    private const string Url = "/api/accounts/{0}/weekly-status";

    private Task<JsonNode> GetAsync(int account, string? week = null) =>
        fixture.Client.GetJsonAsync(string.Format(Url, account) + (week is null ? "" : $"?week={week}"));

    // ---- §6 golden values ---------------------------------------------------------------------------

    [Fact] // §6(a) / G-01
    public async Task Account_1_counts_for_week_2026_07_20()
    {
        var body = await GetAsync(1);

        Assert.Equal("2026-07-20", (string)body["reportedWeek"]!["localStart"]!);
        Assert.Equal("2026-07-20T05:00:00Z", (string)body["reportedWeek"]!["startUtc"]!);
        var account = body["account"]!;
        Assert.Equal(34, (int)account["byType"]!["call_received"]!["value"]!);
        Assert.Equal(12, (int)account["byType"]!["lead_created"]!["value"]!);
        Assert.Equal(7, (int)account["byType"]!["appointment_set"]!["value"]!);
        Assert.Equal(53, (int)account["total"]!["value"]!);
    }

    [Fact] // §6(c) / G-06
    public async Task Account_1_missed_call_rate_is_typical()
    {
        var rate = (await GetAsync(1))["account"]!["missedCallRate"]!;

        Assert.Equal(25.0, (double)rate["value"]!, precision: 4);
        Assert.Equal(24.8771, (double)rate["median"]!, precision: 4);
        Assert.Equal(19.4643, (double)rate["p25"]!, precision: 4);
        Assert.Equal(29.9647, (double)rate["p75"]!, precision: 4);
        Assert.Equal("typical", (string)rate["status"]!);
        Assert.Equal(32, (int)rate["knownOutcomeCalls"]!);
        Assert.Equal(2, (int)rate["unknownOutcomeCalls"]!);
    }

    [Fact] // §6(b) / G-02
    public async Task Account_6_Site_C_is_typical()
    {
        var total = (await GetAsync(6)).Location("Site C")["total"]!;

        Assert.Equal(6, (int)total["value"]!);
        Assert.Equal(6.0, (double)total["median"]!);
        Assert.Equal(3.75, (double)total["p25"]!);
        Assert.Equal(8.75, (double)total["p75"]!);
        Assert.Equal("typical", (string)total["status"]!);
    }

    [Fact] // §6(d) / G-09: baseline crosses the 2026-03-08 DST change (IANA zones on Linux in CI too)
    public async Task Account_1_week_2026_03_30_is_typical()
    {
        var total = (await GetAsync(1, "2026-03-30"))["account"]!["total"]!;

        Assert.Equal(61, (int)total["value"]!);
        Assert.Equal(47.5, (double)total["median"]!);
        Assert.Equal(42.25, (double)total["p25"]!);
        Assert.Equal(51.0, (double)total["p75"]!);
        Assert.Equal("typical", (string)total["status"]!);
    }

    // ---- Rules across the whole dataset -------------------------------------------------------------

    [Fact] // G-04: D6 with D17 order over all 69 locations, week 2026-07-20
    public async Task Location_statuses_across_all_accounts_match_the_evidence()
    {
        var statuses = new List<string>();
        for (var account = 1; account <= 19; account++)
        {
            statuses.AddRange((await GetAsync(account))["locations"]!.AsArray().Select(l => (string)l!["total"]!["status"]!));
        }

        Assert.Equal(69, statuses.Count);
        Assert.Equal(
            new Dictionary<string, int> { ["typical"] = 40, ["above"] = 15, ["below"] = 8, ["low_volume"] = 6 },
            statuses.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count()));
    }

    [Fact] // C2 / G-05 with D21: only these accounts get a missed-call status
    public async Task Only_accounts_1_4_6_and_12_get_a_missed_call_status()
    {
        var withRate = new List<int>();
        for (var account = 1; account <= 19; account++)
        {
            if ((await GetAsync(account))["account"]!["missedCallRate"]!["status"] is not null)
            {
                withRate.Add(account);
            }
        }

        Assert.Equal([1, 4, 6, 12], withRate);
    }

    // ---- SQL details --------------------------------------------------------------------------------

    [Fact] // G-03 / D8: one duplicate pair in this week, counted once
    public async Task Duplicates_are_counted_once()
    {
        var siteC = (await GetAsync(1, "2026-07-06")).Location("Site C");

        Assert.Equal(4, (int)siteC["total"]!["value"]!);
    }

    [Fact] // E-13 / D11: a location with 0 events in the reported week is still listed
    public async Task Location_with_no_events_is_listed_with_zero()
    {
        var body = await GetAsync(6, "2026-04-13");

        Assert.Equal(15, body["locations"]!.AsArray().Count);
        Assert.Equal(0, (int)body.Location("Site G")["total"]!["value"]!);
    }

    [Fact] // D20: Below (largest gap first), Above (largest gap first), Low volume, Typical; ties by name
    public async Task Account_6_locations_are_in_D20_order()
    {
        var rows = (await GetAsync(6))["locations"]!.AsArray()
            .Select(l => (Name: (string)l!["location"]!, Total: l["total"]!))
            .ToList();

        var expected = rows
            .OrderBy(r => Rank((string?)r.Total["status"]))
            .ThenByDescending(r => Gap(r.Total))
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => r.Name);
        Assert.Equal(15, rows.Count);
        Assert.Equal(expected, rows.Select(r => r.Name));
        Assert.Equal(["Site M", "Site O"], rows.Take(2).Select(r => r.Name)); // G-04: both Above, gap 3.5

        static int Rank(string? status) => status switch
        {
            "below" => 0, "above" => 1, "low_volume" => 2, "typical" => 3, _ => 4,
        };
        static double Gap(JsonNode total) => (string?)total["status"] switch
        {
            "below" => (double)total["median"]! - (int)total["value"]!,
            "above" => (int)total["value"]! - (double)total["median"]!,
            _ => 0,
        };
    }

    // ---- §5 edge cases ------------------------------------------------------------------------------

    [Theory] // E-19 / D11 / D29: account 20 has no events
    [InlineData(null)]
    [InlineData("2026-07-20")]
    public async Task Account_20_returns_the_empty_result(string? week)
    {
        var body = await GetAsync(20, week);

        Assert.Empty(body["locations"]!.AsArray());
        Assert.Null(body["availableWeeks"]);
        Assert.Null(body["account"]!["total"]!["status"]);
    }

    [Theory] // D18 and model binding
    [InlineData("999", HttpStatusCode.NotFound)]
    [InlineData("abc", HttpStatusCode.BadRequest)]
    public async Task Unknown_or_invalid_account(string account, HttpStatusCode expected)
    {
        var response = await fixture.Client.GetAsync(string.Format(Url, account));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact] // §5 Sparse calls / G-05
    public async Task Account_5_rate_has_too_few_calls()
    {
        var reasons = (await GetAsync(5))["account"]!["missedCallRate"]!["reasons"]!.AsArray();

        var reason = Assert.Single(reasons)!;
        Assert.Equal("reported_week_too_few_calls", (string)reason["code"]!);
        Assert.Equal(18, (int)reason["actual"]!);
        Assert.Equal(20, (int)reason["required"]!);
    }

    [Fact] // D29: no week is the same as the default week
    public async Task Requesting_the_default_week_returns_the_same_body()
    {
        var byDefault = await GetAsync(1);
        var requested = await GetAsync(1, "2026-07-20");

        Assert.Equal(byDefault.ToJsonString(), requested.ToJsonString());
    }

    [Theory] // D29: Tuesday, partial week, before the first complete week
    [InlineData("2026-07-21")]
    [InlineData("2026-07-27")]
    [InlineData("2026-01-26")]
    public async Task Invalid_week_is_rejected(string week)
    {
        var response = await fixture.Client.GetAsync(string.Format(Url, 1) + $"?week={week}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact] // D27 / G-08: 4 of 8 baseline weeks
    public async Task Early_week_has_values_but_no_status()
    {
        var total = (await GetAsync(1, "2026-03-02"))["account"]!["total"]!;

        Assert.Equal(51, (int)total["value"]!);
        Assert.Null(total["status"]);
        var reason = Assert.Single(total["reasons"]!.AsArray())!;
        Assert.Equal("insufficient_history", (string)reason["code"]!);
        Assert.Equal(4, (int)reason["actual"]!);
        Assert.Equal(8, (int)reason["required"]!);
    }
}
