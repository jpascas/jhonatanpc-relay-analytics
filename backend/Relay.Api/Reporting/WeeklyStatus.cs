namespace Relay.Api.Reporting;

/// <summary>D5: the event types shown per location and evaluated per account, in response order.</summary>
public static class EventTypes
{
    public const string CallReceived = "call_received";
    public const string LeadCreated = "lead_created";
    public const string AppointmentSet = "appointment_set";

    public static readonly IReadOnlyList<string> All = [CallReceived, LeadCreated, AppointmentSet];
}

/// <summary>
/// One row of the weekly counts query: a location in one week, zero-filled. <see cref="WeekIndex"/> is the position in
/// <c>plan.Baseline</c>, and <c>plan.Baseline.Count</c> is the reported week.
/// </summary>
public sealed record LocationWeekCounts(
    string Location, int WeekIndex, int Calls, int Leads, int Appointments, int Total,
    int KnownCalls, int MissedCalls, int UnknownCalls);

/// <summary>§12 API response contract (D24).</summary>
public sealed record WeeklyStatusResponse(
    int AccountId,
    string AccountName,
    string Timezone,
    WeekRange ReportedWeek,
    IReadOnlyList<DateOnly> BaselineWeeks,
    AvailableWeeks? AvailableWeeks,
    StatusThresholds Thresholds,
    AccountStatus Account,
    IReadOnlyList<LocationStatus> Locations);

public sealed record AccountStatus(
    CountEvaluation Total, IReadOnlyDictionary<string, CountEvaluation> ByType, RateEvaluation MissedCallRate);

/// <summary>D5: status on the location total only; per-type counts have no status.</summary>
public sealed record LocationStatus(string Location, CountEvaluation Total, IReadOnlyDictionary<string, int> ByType);

/// <summary>Builds the response from the counts; no I/O.</summary>
public static class WeeklyStatusAssembler
{
    public static WeeklyStatusResponse Assemble(
        int accountId, string accountName, string timezone, WeekPlan plan,
        IReadOnlyList<LocationWeekCounts> rows, StatusThresholds thresholds)
    {
        var weekCount = plan.Baseline.Count + 1;

        var locations = rows
            .GroupBy(r => r.Location)
            .Select(g =>
            {
                var weeks = ByWeek(g, weekCount);
                var reported = weeks[^1];
                return new LocationStatus(
                    g.Key,
                    BaselineEvaluator.EvaluateCount(reported.Total, Baseline(weeks, w => w.Total), thresholds),
                    new Dictionary<string, int>
                    {
                        [EventTypes.CallReceived] = reported.Calls,
                        [EventTypes.LeadCreated] = reported.Leads,
                        [EventTypes.AppointmentSet] = reported.Appointments,
                    });
            })
            .OrderBy(l => l.Total, D20Order)
            .ThenBy(l => l.Location, StringComparer.Ordinal)
            .ToList();

        var account = ByWeek(rows, weekCount);
        var accountReported = account[^1];
        var accountStatus = new AccountStatus(
            BaselineEvaluator.EvaluateCount(accountReported.Total, Baseline(account, w => w.Total), thresholds),
            new Dictionary<string, CountEvaluation>
            {
                [EventTypes.CallReceived] = BaselineEvaluator.EvaluateCount(accountReported.Calls, Baseline(account, w => w.Calls), thresholds),
                [EventTypes.LeadCreated] = BaselineEvaluator.EvaluateCount(accountReported.Leads, Baseline(account, w => w.Leads), thresholds),
                [EventTypes.AppointmentSet] = BaselineEvaluator.EvaluateCount(accountReported.Appointments, Baseline(account, w => w.Appointments), thresholds),
            },
            BaselineEvaluator.EvaluateMissedCallRate(
                Calls(accountReported), account.Take(weekCount - 1).Select(Calls).ToList(), thresholds));

        return new WeeklyStatusResponse(
            accountId, accountName, timezone,
            plan.Reported,
            plan.Baseline.Select(w => w.LocalStart).ToList(),
            plan.AvailableWeeks,
            thresholds,
            accountStatus,
            locations);
    }

    /// <summary>Sums rows per week index; weeks without rows are 0.</summary>
    private static WeekTotals[] ByWeek(IEnumerable<LocationWeekCounts> rows, int weekCount)
    {
        var weeks = new WeekTotals[weekCount];
        foreach (var r in rows)
        {
            var w = weeks[r.WeekIndex];
            weeks[r.WeekIndex] = new WeekTotals(
                w.Calls + r.Calls, w.Leads + r.Leads, w.Appointments + r.Appointments, w.Total + r.Total,
                w.KnownCalls + r.KnownCalls, w.MissedCalls + r.MissedCalls, w.UnknownCalls + r.UnknownCalls);
        }
        return weeks;
    }

    private static IReadOnlyList<int> Baseline(WeekTotals[] weeks, Func<WeekTotals, int> pick) =>
        weeks.Take(weeks.Length - 1).Select(pick).ToList();

    private static WeekCalls Calls(WeekTotals w) => new(w.KnownCalls, w.MissedCalls, w.UnknownCalls);

    private readonly record struct WeekTotals(
        int Calls, int Leads, int Appointments, int Total, int KnownCalls, int MissedCalls, int UnknownCalls);

    /// <summary>
    /// D20: Below (largest m−v first), Above (largest v−m first), Low volume, Typical; ties by name (ThenBy).
    /// Locations without a status (D27) go last.
    /// </summary>
    private static readonly IComparer<CountEvaluation> D20Order = Comparer<CountEvaluation>.Create((a, b) =>
    {
        var byStatus = Rank(a.Status).CompareTo(Rank(b.Status));
        return byStatus != 0 ? byStatus : Gap(b).CompareTo(Gap(a));

        static int Rank(Status? s) => s switch
        {
            Status.Below => 0, Status.Above => 1, Status.LowVolume => 2, Status.Typical => 3, _ => 4,
        };
        static double Gap(CountEvaluation e) => e.Status switch
        {
            Status.Below => e.Median!.Value - e.Value,
            Status.Above => e.Value - e.Median!.Value,
            _ => 0,
        };
    });
}
