using Relay.Api.Reporting;

namespace Relay.Api.Tests.Reporting;

public class WeekPlannerTests
{
    private static readonly DateTimeOffset DataNow = new(2026, 7, 27, 22, 20, 34, TimeSpan.Zero); // E-01
    private static readonly TimeZoneInfo Chicago = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
    private static readonly TimeZoneInfo LosAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    // A first event on Sunday 2026-02-01 (local), so 2026-02-02 is the first complete week (G-07: account 1).
    private static readonly DateTime Account1FirstEvent = new(2026, 2, 1, 15, 0, 0, DateTimeKind.Utc);

    private static WeekPlan Plan(string? week, DateTime? firstEvent, TimeZoneInfo zone)
    {
        var result = WeekPlanner.Plan(week, firstEvent, DataNow, zone, baselineWeeks: 8);
        Assert.Null(result.Error);
        return result.Plan!;
    }

    // ---- availableWeeks -----------------------------------------------------------------------

    [Fact] // G-07 with E-01's first event: id 1, account 5, 2026-02-01 10:57:44 UTC
    public void Account_5_available_weeks_are_2026_02_02_to_2026_07_20()
    {
        var available = WeekPlanner.Available(new DateTime(2026, 2, 1, 10, 57, 44, DateTimeKind.Utc), DataNow, LosAngeles);

        Assert.Equal(new AvailableWeeks(new DateOnly(2026, 2, 2), new DateOnly(2026, 7, 20)), available);
    }

    [Theory] // The first complete week starts at or after the first event.
    [InlineData("2026-02-03T12:00:00Z", "2026-02-09")] // Tuesday: that week is partial
    [InlineData("2026-02-09T06:00:00Z", "2026-02-09")] // exactly local Monday 00:00 (CST): that week is complete
    public void Earliest_week_is_the_first_one_that_starts_after_the_first_event(string firstEvent, string expected)
    {
        var available = WeekPlanner.Available(DateTime.Parse(firstEvent).ToUniversalTime(), DataNow, Chicago);

        Assert.Equal(DateOnly.Parse(expected), available!.Earliest);
    }

    [Fact] // E-19: account 20 has no events
    public void No_events_means_no_available_weeks()
    {
        Assert.Null(WeekPlanner.Available(null, DataNow, LosAngeles));
    }

    [Fact] // Events only in the still-running week: no complete week yet
    public void Events_only_in_the_current_week_mean_no_available_weeks()
    {
        Assert.Null(WeekPlanner.Available(new DateTime(2026, 7, 27, 18, 0, 0, DateTimeKind.Utc), DataNow, Chicago));
    }

    // ---- reported week and baseline -----------------------------------------------------------

    [Fact] // D2 + G-02
    public void Default_week_is_the_latest_complete_week_with_8_baseline_weeks()
    {
        var plan = Plan(null, Account1FirstEvent, Chicago);

        Assert.Equal(new DateOnly(2026, 7, 20), plan.Reported.LocalStart);
        Assert.Equal(new DateOnly(2026, 5, 25), plan.Baseline[0].LocalStart);
        Assert.Equal(new DateOnly(2026, 7, 13), plan.Baseline[^1].LocalStart);
        Assert.Equal(8, plan.Baseline.Count);
        Assert.Equal(new AvailableWeeks(new DateOnly(2026, 2, 2), new DateOnly(2026, 7, 20)), plan.AvailableWeeks);
    }

    [Fact] // §5 Requested week: no `week` gives the same result as ?week=2026-07-20
    public void Requesting_the_default_week_gives_the_same_plan()
    {
        var byDefault = Plan(null, Account1FirstEvent, Chicago);
        var requested = Plan("2026-07-20", Account1FirstEvent, Chicago);

        Assert.Equal(byDefault.Reported, requested.Reported);
        Assert.Equal(byDefault.Baseline, requested.Baseline);
        Assert.Equal(byDefault.AvailableWeeks, requested.AvailableWeeks);
    }

    [Fact] // D27 / G-08: account 1, week 2026-03-02 has 4 of 8 complete baseline weeks
    public void Early_week_keeps_only_complete_baseline_weeks()
    {
        var plan = Plan("2026-03-02", Account1FirstEvent, Chicago);

        Assert.Equal(
            new[] { new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 9), new DateOnly(2026, 2, 16), new DateOnly(2026, 2, 23) },
            plan.Baseline.Select(w => w.LocalStart));
        Assert.Equal(new DateTime(2026, 3, 2, 6, 0, 0, DateTimeKind.Utc), plan.Reported.StartUtc);
        Assert.Equal(new DateTime(2026, 3, 9, 5, 0, 0, DateTimeKind.Utc), plan.Reported.EndUtc);
    }

    [Fact]
    public void First_complete_week_has_an_empty_baseline()
    {
        Assert.Empty(Plan("2026-02-02", Account1FirstEvent, Chicago).Baseline);
    }

    [Theory] // §5 Requested week → 400
    [InlineData("2026-07-21")] // Tuesday
    [InlineData("2026-07-27")] // partial week, after the latest complete week
    [InlineData("2026-01-26")] // before the first complete week
    [InlineData("2026-7-20")]  // not YYYY-MM-DD
    [InlineData("yesterday")]
    [InlineData("")]
    public void Invalid_or_out_of_range_week_is_an_error(string week)
    {
        var result = WeekPlanner.Plan(week, Account1FirstEvent, DataNow, Chicago, baselineWeeks: 8);

        Assert.Null(result.Plan);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Theory] // D29: account 20 → any valid Monday is 200 with the empty result and availableWeeks null
    [InlineData(null, "2026-07-20")]
    [InlineData("2026-07-20", "2026-07-20")]
    [InlineData("2020-01-06", "2020-01-06")]
    public void Account_without_complete_weeks_accepts_any_Monday(string? week, string expectedReported)
    {
        var plan = Plan(week, null, LosAngeles);

        Assert.Equal(DateOnly.Parse(expectedReported), plan.Reported.LocalStart);
        Assert.Empty(plan.Baseline);
        Assert.Null(plan.AvailableWeeks);
    }

    [Fact]
    public void Account_without_complete_weeks_still_rejects_a_non_Monday()
    {
        var result = WeekPlanner.Plan("2026-07-21", null, DataNow, LosAngeles, baselineWeeks: 8);

        Assert.NotNull(result.Error);
    }
}
