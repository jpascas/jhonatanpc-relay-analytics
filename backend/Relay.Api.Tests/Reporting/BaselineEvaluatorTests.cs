using Relay.Api.Reporting;

namespace Relay.Api.Tests.Reporting;

public class BaselineEvaluatorTests
{
    private static readonly StatusThresholds Defaults = new();

    private static StatusReason Reason(string code, int actual, int required) => new(code, actual, required);

    // ---- Counts: D4 percentiles, D6 rule, D17 order --------------------------------------------

    [Fact] // §6(b) / G-02: account 6, Site C, week 2026-07-20
    public void Account_6_Site_C_is_typical()
    {
        var result = BaselineEvaluator.EvaluateCount(6, [4, 67, 11, 3, 6, 6, 8, 3], Defaults);

        Assert.Equal(6, result.Value);
        Assert.Equal(6.0, result.Median);
        Assert.Equal(3.75, result.P25);
        Assert.Equal(8.75, result.P75);
        Assert.Equal(Status.Typical, result.Status);
        Assert.Empty(result.Reasons);
    }

    [Fact] // §6(d) / G-09: outside the range but under the gap max(3, 0.3·47.5) = 14.25
    public void Account_1_week_2026_03_30_is_typical_because_the_gap_is_too_small()
    {
        var result = BaselineEvaluator.EvaluateCount(61, [43, 51, 40, 50, 51, 52, 36, 45], Defaults);

        Assert.Equal(47.5, result.Median);
        Assert.Equal(42.25, result.P25);
        Assert.Equal(51.0, result.P75);
        Assert.Equal(Status.Typical, result.Status);
    }

    [Theory] // §10 / G-04, week 2026-07-20
    [InlineData("1 / Site C", 9, new[] { 4, 4, 5, 5, 6, 8, 8, 11 }, Status.Above)]
    [InlineData("5 / Site B", 3, new[] { 3, 4, 6, 6, 7, 10, 11, 14 }, Status.Below)]
    [InlineData("8 / Site A", 7, new[] { 8, 8, 10, 10, 10, 11, 12, 13 }, Status.Below)] // m−v = 3 = gap: ≥ flags
    [InlineData("18 / Site A", 3, new[] { 1, 2, 2, 3, 4, 5, 7, 8 }, Status.LowVolume)]
    [InlineData("6 / Site M", 7, new[] { 1, 2, 3, 3, 4, 5, 6, 50 }, Status.Above)]   // D17: Above before Low volume
    public void Extra_test_vectors(string location, int value, int[] baseline, Status expected)
    {
        var result = BaselineEvaluator.EvaluateCount(value, baseline, Defaults);

        Assert.True(expected == result.Status, $"{location}: expected {expected}, got {result.Status}");
    }

    [Fact] // D23: thresholds change the outcome; 1/Site C has v−m = 3.5 < 4
    public void Min_gap_4_makes_1_Site_C_typical()
    {
        var result = BaselineEvaluator.EvaluateCount(9, [4, 4, 5, 5, 6, 8, 8, 11], Defaults with { MinGap = 4 });

        Assert.Equal(5.5, result.Median);
        Assert.Equal(Status.Typical, result.Status);
    }

    [Fact] // D27 / G-08: account 1, week 2026-03-02 has 4 of 8 baseline weeks
    public void Short_baseline_keeps_the_value_and_gives_insufficient_history()
    {
        var result = BaselineEvaluator.EvaluateCount(51, [43, 51, 40, 50], Defaults);

        Assert.Equal(51, result.Value);
        Assert.Null(result.Median);
        Assert.Null(result.P25);
        Assert.Null(result.P75);
        Assert.Null(result.Status);
        Assert.Equal([Reason(StatusReason.InsufficientHistory, 4, 8)], result.Reasons);
    }

    [Fact] // D3/E-13: a 0-event week is a real baseline value
    public void Zero_weeks_count_as_baseline_values()
    {
        var result = BaselineEvaluator.EvaluateCount(0, [0, 0, 0, 0, 0, 0, 0, 0], Defaults);

        Assert.Equal(0.0, result.Median);
        Assert.Equal(Status.LowVolume, result.Status);
    }

    [Fact]
    public void Percentile_inc_interpolates_linearly()
    {
        double[] values = [3, 1, 2, 4]; // unsorted on purpose

        Assert.Equal(1.0, BaselineEvaluator.PercentileInc(values, 0));
        Assert.Equal(1.75, BaselineEvaluator.PercentileInc(values, 0.25));
        Assert.Equal(2.5, BaselineEvaluator.PercentileInc(values, 0.5));
        Assert.Equal(4.0, BaselineEvaluator.PercentileInc(values, 1));
    }

    // ---- Missed-call rate: D7, D9, D21, D25, D30 ----------------------------------------------

    [Fact] // §6(c) / G-06: account 1, week 2026-07-20
    public void Account_1_missed_call_rate_is_typical()
    {
        int[] known = [25, 22, 32, 31, 25, 37, 22, 28];
        int[] missed = [5, 8, 9, 11, 2, 10, 5, 5];
        var baseline = known.Zip(missed, (k, m) => new WeekCalls(k, m, 0)).ToList();

        var result = BaselineEvaluator.EvaluateMissedCallRate(new WeekCalls(32, 8, 2), baseline, Defaults);

        Assert.Equal(25.0, result.Value!.Value, precision: 10);
        Assert.Equal(24.8771, result.Median!.Value, precision: 4);
        Assert.Equal(19.4643, result.P25!.Value, precision: 4);
        Assert.Equal(29.9647, result.P75!.Value, precision: 4);
        Assert.Equal(Status.Typical, result.Status);
        Assert.Equal(32, result.KnownOutcomeCalls);
        Assert.Equal(2, result.UnknownOutcomeCalls);
        Assert.Empty(result.Reasons);
    }

    [Fact] // D25 / G-05: account 3 has 14 known calls and 0 qualifying baseline weeks
    public void Account_3_gets_both_rate_reasons_in_order()
    {
        var baseline = Enumerable.Repeat(new WeekCalls(12, 3, 0), 8).ToList();

        var result = BaselineEvaluator.EvaluateMissedCallRate(new WeekCalls(14, 4, 1), baseline, Defaults);

        Assert.Equal(
            [Reason(StatusReason.ReportedWeekTooFewCalls, 14, 20), Reason(StatusReason.BaselineTooFewWeeks, 0, 5)],
            result.Reasons);
        Assert.Null(result.Value);
        Assert.Null(result.Median);
        Assert.Null(result.P25);
        Assert.Null(result.P75);
        Assert.Null(result.Status);
        Assert.Equal(14, result.KnownOutcomeCalls);
        Assert.Equal(1, result.UnknownOutcomeCalls);
    }

    [Fact] // §5 Sparse calls / G-05: account 5 has 18 known calls, 5 qualifying baseline weeks
    public void Account_5_reported_week_has_too_few_calls()
    {
        var baseline = Enumerable.Repeat(new WeekCalls(25, 5, 0), 5).Concat(Enumerable.Repeat(new WeekCalls(15, 3, 0), 3)).ToList();

        var result = BaselineEvaluator.EvaluateMissedCallRate(new WeekCalls(18, 4, 0), baseline, Defaults);

        Assert.Equal([Reason(StatusReason.ReportedWeekTooFewCalls, 18, 20)], result.Reasons);
        Assert.Null(result.Status);
    }

    [Fact] // §5 Sparse calls / G-05: account 2 has 20 known calls (qualifies), 3 qualifying baseline weeks
    public void Account_2_baseline_has_too_few_qualifying_weeks()
    {
        var baseline = Enumerable.Repeat(new WeekCalls(22, 5, 0), 3).Concat(Enumerable.Repeat(new WeekCalls(19, 4, 0), 5)).ToList();

        var result = BaselineEvaluator.EvaluateMissedCallRate(new WeekCalls(20, 4, 0), baseline, Defaults);

        Assert.Equal([Reason(StatusReason.BaselineTooFewWeeks, 3, 5)], result.Reasons);
        Assert.Null(result.Value);
    }

    [Fact] // D30: 7 of 8 baseline weeks, all qualifying → no rate status
    public void Short_baseline_gives_the_rate_insufficient_history()
    {
        var baseline = Enumerable.Repeat(new WeekCalls(30, 6, 0), 7).ToList();

        var result = BaselineEvaluator.EvaluateMissedCallRate(new WeekCalls(32, 8, 0), baseline, Defaults);

        Assert.Equal([Reason(StatusReason.InsufficientHistory, 7, 8)], result.Reasons);
        Assert.Null(result.Value);
    }

    [Fact] // D30 order: D25's two reasons first, then insufficient_history
    public void All_three_rate_reasons_are_listed_in_order()
    {
        var baseline = Enumerable.Repeat(new WeekCalls(10, 2, 0), 4).ToList();

        var result = BaselineEvaluator.EvaluateMissedCallRate(new WeekCalls(9, 1, 0), baseline, Defaults);

        Assert.Equal(
            [
                Reason(StatusReason.ReportedWeekTooFewCalls, 9, 20),
                Reason(StatusReason.BaselineTooFewWeeks, 0, 5),
                Reason(StatusReason.InsufficientHistory, 4, 8),
            ],
            result.Reasons);
    }

    [Theory] // D7: flag only outside [p25, p75] AND ≥ 10 pp from the median; baseline is 10% every week
    [InlineData(8, Status.Above)]   // 40%: 30 pp above
    [InlineData(3, Status.Typical)] // 15%: above P75 but only 5 pp
    [InlineData(0, Status.Below)]   // 0%: exactly 10 pp below, ≥ flags
    public void Rate_status_needs_range_breach_and_gap(int missedOf20, Status expected)
    {
        var baseline = Enumerable.Repeat(new WeekCalls(20, 2, 0), 8).ToList();

        var result = BaselineEvaluator.EvaluateMissedCallRate(new WeekCalls(20, missedOf20, 0), baseline, Defaults);

        Assert.Equal(expected, result.Status);
    }

    [Fact] // D21: non-qualifying baseline weeks are left out of the rate baseline
    public void Rate_baseline_uses_only_qualifying_weeks()
    {
        // 5 qualifying weeks at 20% and 3 tiny weeks at 100% that must be ignored.
        var baseline = Enumerable.Repeat(new WeekCalls(20, 4, 0), 5).Concat(Enumerable.Repeat(new WeekCalls(2, 2, 0), 3)).ToList();

        var result = BaselineEvaluator.EvaluateMissedCallRate(new WeekCalls(20, 4, 0), baseline, Defaults);

        Assert.Equal(20.0, result.Median);
        Assert.Equal(20.0, result.P75);
    }
}
