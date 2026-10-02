using Relay.Api.Reporting;

namespace Relay.Api.Tests.Reporting;

public class WeeklyStatusAssemblerTests
{
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly StatusThresholds Defaults = new();
    private static readonly AvailableWeeks Available = new(new DateOnly(2026, 2, 2), new DateOnly(2026, 7, 20));

    private static WeekPlan PlanWithBaseline(int baselineWeeks)
    {
        var all = WeekCalculator.ForReport(NewYork, default, new DateOnly(2026, 7, 20), 8);
        return new WeekPlan(all.Reported, all.Baseline.Skip(8 - baselineWeeks).ToList(), Available);
    }

    /// <summary>Rows for one location: every event is a call with a known outcome, a quarter of them missed.</summary>
    private static IEnumerable<LocationWeekCounts> Location(string name, int[] baseline, int reported) =>
        baseline.Append(reported).Select((n, week) => new LocationWeekCounts(name, week, n, 0, 0, n, n, n / 4, 0));

    private static WeeklyStatusResponse Assemble(WeekPlan plan, IEnumerable<LocationWeekCounts> rows) =>
        WeeklyStatusAssembler.Assemble(6, "Metro Collision Centers", "America/New_York", plan, rows.ToList(), Defaults);

    private static readonly int[] Tens = [10, 10, 10, 10, 10, 10, 10, 10];

    [Fact] // D20: Below by m−v desc, Above by v−m desc, Low volume, Typical; ties by name
    public void Locations_are_in_D20_order()
    {
        var rows = new[]
        {
            Location("Site A", Tens, 10),                     // typical
            Location("Site B", Tens, 0),                      // below, m−v = 10
            Location("Site C", Tens, 5),                      // below, m−v = 5
            Location("Site D", Tens, 20),                     // above, v−m = 10
            Location("Site E", [2, 2, 2, 2, 2, 2, 2, 2], 2),  // low volume
            Location("Site F", Tens, 10),                     // typical, after Site A by name
            Location("Site G", Tens, 25),                     // above, v−m = 15
        }.SelectMany(r => r);

        var response = Assemble(PlanWithBaseline(8), rows);

        Assert.Equal(
            ["Site B", "Site C", "Site G", "Site D", "Site E", "Site A", "Site F"],
            response.Locations.Select(l => l.Location));
        Assert.Equal(
            [Status.Below, Status.Below, Status.Above, Status.Above, Status.LowVolume, Status.Typical, Status.Typical],
            response.Locations.Select(l => l.Total.Status!.Value));
    }

    [Fact] // D5: per-type counts per location, without status; D11: a 0 week still listed
    public void Location_has_per_type_counts_and_zero_weeks_are_listed()
    {
        var rows = Location("Site A", Tens, 10)
            .Concat(Enumerable.Range(0, 9).Select(w => new LocationWeekCounts("Site Z", w, w == 8 ? 0 : 3, 1, 1, w == 8 ? 0 : 5, 3, 0, 0)))
            .Append(new LocationWeekCounts("Site Y", 8, 4, 2, 1, 7, 4, 1, 0))
            .Concat(Enumerable.Range(0, 8).Select(w => new LocationWeekCounts("Site Y", w, 0, 0, 0, 0, 0, 0, 0)));

        var response = Assemble(PlanWithBaseline(8), rows);

        var siteY = response.Locations.Single(l => l.Location == "Site Y");
        Assert.Equal(new Dictionary<string, int> { ["call_received"] = 4, ["lead_created"] = 2, ["appointment_set"] = 1 }, siteY.ByType);
        Assert.Equal(7, siteY.Total.Value);
        Assert.Equal(0, response.Locations.Single(l => l.Location == "Site Z").Total.Value);
    }

    [Fact] // Account level: sums across locations per week; status on total, each type and missed-call rate
    public void Account_values_are_sums_across_locations()
    {
        var rows = Location("Site A", Tens, 12).Concat(Location("Site B", [30, 30, 30, 30, 30, 30, 30, 30], 30));

        var response = Assemble(PlanWithBaseline(8), rows);

        Assert.Equal(42, response.Account.Total.Value);
        Assert.Equal(40.0, response.Account.Total.Median);
        Assert.Equal(42, response.Account.ByType["call_received"].Value);
        Assert.Equal(0, response.Account.ByType["lead_created"].Value);
        Assert.Equal(["call_received", "lead_created", "appointment_set"], response.Account.ByType.Keys);
        // Reported week: 42 known calls, 12/4 + 30/4 = 3 + 7 = 10 missed.
        Assert.Equal(42, response.Account.MissedCallRate.KnownOutcomeCalls);
        Assert.Equal(100.0 * 10 / 42, response.Account.MissedCallRate.Value!.Value, precision: 10);
    }

    [Fact] // D27: no statuses with a short baseline; those locations sort after the rest, by name
    public void Short_baseline_gives_null_statuses()
    {
        var rows = Location("Site B", [10, 10, 10, 10], 10).Concat(Location("Site A", [1, 1, 1, 1], 50));

        var response = Assemble(PlanWithBaseline(4), rows);

        Assert.Equal(["Site A", "Site B"], response.Locations.Select(l => l.Location));
        Assert.All(response.Locations, l => Assert.Null(l.Total.Status));
        Assert.Equal(StatusReason.InsufficientHistory, Assert.Single(response.Account.Total.Reasons).Code);
        Assert.Equal(new DateOnly(2026, 6, 22), response.BaselineWeeks[0]);
    }

    [Fact] // §12: account 20 → same shape, locations [], statuses null
    public void Account_without_rows_has_the_same_shape_and_no_locations()
    {
        var all = WeekCalculator.ForReport(NewYork, default, new DateOnly(2026, 7, 20), 8);
        var plan = new WeekPlan(all.Reported, [], null);

        var response = WeeklyStatusAssembler.Assemble(20, "Quiet Harbor Spa", "America/Los_Angeles", plan, [], Defaults);

        Assert.Empty(response.Locations);
        Assert.Null(response.AvailableWeeks);
        Assert.Empty(response.BaselineWeeks);
        Assert.Equal(0, response.Account.Total.Value);
        Assert.Null(response.Account.Total.Status);
        Assert.Null(response.Account.MissedCallRate.Status);
        Assert.Equal(3, response.Account.ByType.Count);
    }

    [Fact] // D23: thresholds and the plan are echoed
    public void Response_echoes_thresholds_and_weeks()
    {
        var plan = PlanWithBaseline(8);

        var response = Assemble(plan, Location("Site A", Tens, 10));

        Assert.Same(Defaults, response.Thresholds);
        Assert.Equal(plan.Reported, response.ReportedWeek);
        Assert.Equal(plan.Baseline.Select(w => w.LocalStart), response.BaselineWeeks);
        Assert.Equal(Available, response.AvailableWeeks);
        Assert.Equal((6, "Metro Collision Centers", "America/New_York"), (response.AccountId, response.AccountName, response.Timezone));
    }
}
