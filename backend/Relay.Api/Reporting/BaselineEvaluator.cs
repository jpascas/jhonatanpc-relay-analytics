namespace Relay.Api.Reporting;

/// <summary>D26: one status vocabulary for counts and the missed-call rate.</summary>
public enum Status
{
    Below,
    Above,
    LowVolume,
    Typical,
}

/// <summary>Why a status is missing (§12): <c>actual</c> of <c>required</c>.</summary>
public sealed record StatusReason(string Code, int Actual, int Required)
{
    public const string ReportedWeekTooFewCalls = "reported_week_too_few_calls";
    public const string BaselineTooFewWeeks = "baseline_too_few_weeks";
    public const string InsufficientHistory = "insufficient_history";
}

/// <summary>A count compared with its baseline. Median/P25/P75/Status are null when <see cref="Reasons"/> is not empty.</summary>
public sealed record CountEvaluation(
    int Value, double? Median, double? P25, double? P75, Status? Status, IReadOnlyList<StatusReason> Reasons);

/// <summary>Known-outcome and missed calls in one week (D9: unknown outcomes are neither).</summary>
public readonly record struct WeekCalls(int KnownOutcome, int Missed, int UnknownOutcome);

/// <summary>Missed-call rate in percent. Value/Median/P25/P75/Status are null when <see cref="Reasons"/> is not empty.</summary>
public sealed record RateEvaluation(
    double? Value, double? Median, double? P25, double? P75, Status? Status,
    int KnownOutcomeCalls, int UnknownOutcomeCalls, IReadOnlyList<StatusReason> Reasons);

/// <summary>Pure baseline rules: D4 (PERCENTILE.INC), D6 + D17 (counts), D7 + D21 + D25 + D27 + D30 (rate).</summary>
public static class BaselineEvaluator
{
    /// <summary>Excel PERCENTILE.INC: linear interpolation at rank p·(n−1) of the sorted values.</summary>
    public static double PercentileInc(IReadOnlyList<double> values, double p)
    {
        var sorted = values.Order().ToArray();
        var h = p * (sorted.Length - 1);
        var lower = (int)Math.Floor(h);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (h - lower) * (sorted[upper] - sorted[lower]);
    }

    /// <summary>D6 in D17 order: Below, Above, Low volume, Typical. D27: no status without a full baseline.</summary>
    public static CountEvaluation EvaluateCount(int value, IReadOnlyList<int> baseline, StatusThresholds thresholds)
    {
        if (baseline.Count < thresholds.BaselineWeeks)
        {
            return new CountEvaluation(value, null, null, null, null,
                [new StatusReason(StatusReason.InsufficientHistory, baseline.Count, thresholds.BaselineWeeks)]);
        }

        var (median, p25, p75) = Quartiles(baseline.Select(v => (double)v).ToList());
        var gap = Math.Max(thresholds.MinGap, thresholds.RelativeGap * median);
        var status =
            value < p25 && median - value >= gap ? Status.Below :
            value > p75 && value - median >= gap ? Status.Above :
            median < thresholds.LowVolumeMedian ? Status.LowVolume :
            Status.Typical;
        return new CountEvaluation(value, median, p25, p75, status, []);
    }

    /// <summary>
    /// D7: missed ÷ known-outcome calls, in percent. D21: only baseline weeks with enough known calls count, and
    /// enough of them are needed. D25/D30: every failed check is listed, in a fixed order.
    /// </summary>
    public static RateEvaluation EvaluateMissedCallRate(
        WeekCalls reported, IReadOnlyList<WeekCalls> baseline, StatusThresholds thresholds)
    {
        var qualifying = baseline.Where(w => w.KnownOutcome >= thresholds.MinKnownCalls).ToList();

        var reasons = new List<StatusReason>();
        if (reported.KnownOutcome < thresholds.MinKnownCalls)
        {
            reasons.Add(new StatusReason(StatusReason.ReportedWeekTooFewCalls, reported.KnownOutcome, thresholds.MinKnownCalls));
        }
        if (qualifying.Count < thresholds.MinRateBaselineWeeks)
        {
            reasons.Add(new StatusReason(StatusReason.BaselineTooFewWeeks, qualifying.Count, thresholds.MinRateBaselineWeeks));
        }
        if (baseline.Count < thresholds.BaselineWeeks)
        {
            reasons.Add(new StatusReason(StatusReason.InsufficientHistory, baseline.Count, thresholds.BaselineWeeks));
        }
        if (reasons.Count > 0)
        {
            return new RateEvaluation(null, null, null, null, null, reported.KnownOutcome, reported.UnknownOutcome, reasons);
        }

        var value = RatePercent(reported);
        var (median, p25, p75) = Quartiles(qualifying.Select(RatePercent).ToList());
        var status =
            value < p25 && median - value >= thresholds.RateGapPp ? Status.Below :
            value > p75 && value - median >= thresholds.RateGapPp ? Status.Above :
            Status.Typical;
        return new RateEvaluation(value, median, p25, p75, status, reported.KnownOutcome, reported.UnknownOutcome, []);
    }

    private static double RatePercent(WeekCalls week) => 100.0 * week.Missed / week.KnownOutcome;

    private static (double Median, double P25, double P75) Quartiles(IReadOnlyList<double> values) =>
        (PercentileInc(values, 0.5), PercentileInc(values, 0.25), PercentileInc(values, 0.75));
}
