using Relay.Api.Reporting;

namespace Relay.Api.Tests.Reporting;

public class WeekCalculatorTests
{
    // E-01: latest occurred_at in the seed, the default "now" (D1).
    private static readonly DateTimeOffset DataNow = new(2026, 7, 27, 22, 20, 34, TimeSpan.Zero);

    private static TimeZoneInfo Zone(string ianaId) => TimeZoneInfo.FindSystemTimeZoneById(ianaId);

    private static DateTime Utc(int y, int mo, int d, int h) => new(y, mo, d, h, 0, 0, DateTimeKind.Utc);

    // E-05: the 19 accounts with events and their zones (account 20 has no events, E-19).
    public static TheoryData<int, string> AccountsWithEvents => new()
    {
        { 1, "America/Chicago" }, { 2, "America/New_York" }, { 3, "America/Denver" }, { 4, "America/Chicago" },
        { 5, "America/Los_Angeles" }, { 6, "America/New_York" }, { 7, "America/Phoenix" }, { 8, "America/Chicago" },
        { 9, "America/Denver" }, { 10, "America/Chicago" }, { 11, "America/New_York" }, { 12, "America/Los_Angeles" },
        { 13, "America/Chicago" }, { 14, "America/New_York" }, { 15, "America/Phoenix" }, { 16, "America/Chicago" },
        { 17, "America/Los_Angeles" }, { 18, "UTC" }, { 19, "America/New_York" },
    };

    [Theory]
    [MemberData(nameof(AccountsWithEvents))]
    public void Default_reported_week_is_2026_07_20_for_every_account(int accountId, string zone)
    {
        var week = WeekCalculator.LastCompleteWeek(DataNow, Zone(zone));

        Assert.True(new DateOnly(2026, 7, 20) == week, $"account {accountId} ({zone}): got {week}");
    }

    [Theory] // E-22
    [InlineData("UTC", 0)]               // account 18
    [InlineData("America/Chicago", 5)]   // account 1
    [InlineData("America/New_York", 4)]  // account 6
    public void Week_2026_07_20_edges(string zone, int offsetHours)
    {
        var week = WeekCalculator.Week(new DateOnly(2026, 7, 20), Zone(zone));

        Assert.Equal(Utc(2026, 7, 20, offsetHours), week.StartUtc);
        Assert.Equal(Utc(2026, 7, 27, offsetHours), week.EndUtc);
        Assert.Equal(DateTimeKind.Utc, week.StartUtc.Kind);
    }

    [Fact] // G-08: the week containing the 2026-03-08 DST change is 167 hours long
    public void Account_1_week_2026_03_02_spans_the_DST_change()
    {
        var week = WeekCalculator.Week(new DateOnly(2026, 3, 2), Zone("America/Chicago"));

        Assert.Equal(Utc(2026, 3, 2, 6), week.StartUtc);
        Assert.Equal(Utc(2026, 3, 9, 5), week.EndUtc);
    }

    [Fact] // G-02: baseline for 2026-07-20 is 2026-05-25 → 2026-07-13, oldest first
    public void Default_report_has_8_contiguous_baseline_weeks_before_the_reported_week()
    {
        var weeks = WeekCalculator.ForReport(Zone("America/New_York"), DataNow, requestedWeek: null, baselineWeeks: 8);

        Assert.Equal(new DateOnly(2026, 7, 20), weeks.Reported.LocalStart);
        Assert.Equal(
            Enumerable.Range(0, 8).Select(i => new DateOnly(2026, 5, 25).AddDays(7 * i)),
            weeks.Baseline.Select(w => w.LocalStart));
        var all = weeks.Baseline.Append(weeks.Reported).ToList();
        for (var i = 1; i < all.Count; i++)
        {
            Assert.Equal(all[i - 1].EndUtc, all[i].StartUtc);
        }
    }

    [Fact] // G-09: requested week 2026-03-30, baseline crosses the DST change
    public void Requested_week_2026_03_30_has_DST_aware_baseline_edges()
    {
        var weeks = WeekCalculator.ForReport(Zone("America/Chicago"), DataNow, new DateOnly(2026, 3, 30), baselineWeeks: 8);

        Assert.Equal(new WeekRange(new DateOnly(2026, 3, 30), Utc(2026, 3, 30, 5), Utc(2026, 4, 6, 5)), weeks.Reported);
        Assert.Equal(
            new[]
            {
                new WeekRange(new DateOnly(2026, 2, 2), Utc(2026, 2, 2, 6), Utc(2026, 2, 9, 6)),
                new WeekRange(new DateOnly(2026, 2, 9), Utc(2026, 2, 9, 6), Utc(2026, 2, 16, 6)),
                new WeekRange(new DateOnly(2026, 2, 16), Utc(2026, 2, 16, 6), Utc(2026, 2, 23, 6)),
                new WeekRange(new DateOnly(2026, 2, 23), Utc(2026, 2, 23, 6), Utc(2026, 3, 2, 6)),
                new WeekRange(new DateOnly(2026, 3, 2), Utc(2026, 3, 2, 6), Utc(2026, 3, 9, 5)),
                new WeekRange(new DateOnly(2026, 3, 9), Utc(2026, 3, 9, 5), Utc(2026, 3, 16, 5)),
                new WeekRange(new DateOnly(2026, 3, 16), Utc(2026, 3, 16, 5), Utc(2026, 3, 23, 5)),
                new WeekRange(new DateOnly(2026, 3, 23), Utc(2026, 3, 23, 5), Utc(2026, 3, 30, 5)),
            },
            weeks.Baseline);
    }

    [Theory] // A week is complete once now reaches its end (half-open range).
    [InlineData("2026-07-27T05:00:00Z", "2026-07-20")] // Chicago Mon 00:00 exactly: week of 07-20 just ended
    [InlineData("2026-07-27T04:59:59Z", "2026-07-13")] // Chicago Sun 23:59:59: week of 07-20 still running
    public void Last_complete_week_at_the_Monday_boundary(string nowUtc, string expected)
    {
        var week = WeekCalculator.LastCompleteWeek(DateTimeOffset.Parse(nowUtc), Zone("America/Chicago"));

        Assert.Equal(DateOnly.Parse(expected), week);
    }

    [Fact]
    public void Requested_week_that_is_not_a_Monday_is_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            WeekCalculator.ForReport(Zone("America/Chicago"), DataNow, new DateOnly(2026, 7, 21), baselineWeeks: 8));

        Assert.Equal("requestedWeek", ex.ParamName);
    }
}
