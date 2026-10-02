using System.Globalization;

namespace Relay.Api.Reporting;

/// <summary>D29: the weeks an account can be reported on: first and last complete local week.</summary>
public sealed record AvailableWeeks(DateOnly Earliest, DateOnly Latest);

/// <summary>The reported week, its complete baseline weeks (oldest first, D27) and the available range.</summary>
public sealed record WeekPlan(WeekRange Reported, IReadOnlyList<WeekRange> Baseline, AvailableWeeks? AvailableWeeks);

/// <summary>Either a plan or a client error (HTTP 400) message.</summary>
public sealed record WeekPlanResult(WeekPlan? Plan, string? Error);

/// <summary>D2/D27/D29: resolves the optional <c>week</c> parameter against the account's data and "now".</summary>
public static class WeekPlanner
{
    /// <summary>
    /// Earliest = first local week that starts at or after the account's first event (G-07);
    /// latest = last week complete at "now" (D2). Null when there are no events or no complete week yet.
    /// </summary>
    public static AvailableWeeks? Available(DateTime? firstEventUtc, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (firstEventUtc is not { } first)
        {
            return null;
        }

        var firstUtc = DateTime.SpecifyKind(first, DateTimeKind.Utc);
        var monday = WeekCalculator.WeekContaining(new DateTimeOffset(firstUtc), zone);
        var earliest = WeekCalculator.Week(monday, zone).StartUtc >= firstUtc ? monday : monday.AddDays(7);
        var latest = WeekCalculator.LastCompleteWeek(now, zone);
        return earliest <= latest ? new AvailableWeeks(earliest, latest) : null;
    }

    public static WeekPlanResult Plan(
        string? requestedWeek, DateTime? firstEventUtc, DateTimeOffset now, TimeZoneInfo zone, int baselineWeeks)
    {
        DateOnly? requested = null;
        if (requestedWeek is not null)
        {
            if (!DateOnly.TryParseExact(requestedWeek, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return Fail($"Query parameter 'week' must be a date in YYYY-MM-DD format (was '{requestedWeek}').");
            }
            if (parsed.DayOfWeek != DayOfWeek.Monday)
            {
                return Fail($"Query parameter 'week' must be a Monday ({parsed:yyyy-MM-dd} is a {parsed.DayOfWeek}).");
            }
            requested = parsed;
        }

        var available = Available(firstEventUtc, now, zone);
        if (requested is { } week && available is not null && (week < available.Earliest || week > available.Latest))
        {
            return Fail($"Week {week:yyyy-MM-dd} is outside the available weeks " +
                        $"{available.Earliest:yyyy-MM-dd} to {available.Latest:yyyy-MM-dd}.");
        }

        // No complete weeks (e.g. no events): any valid Monday gives the empty result (D29).
        var reported = requested ?? available?.Latest ?? WeekCalculator.LastCompleteWeek(now, zone);
        var weeks = WeekCalculator.ForReport(zone, now, reported, baselineWeeks);
        IReadOnlyList<WeekRange> baseline = available is null
            ? []
            : weeks.Baseline.Where(w => w.LocalStart >= available.Earliest).ToList();
        return new WeekPlanResult(new WeekPlan(weeks.Reported, baseline, available), null);
    }

    private static WeekPlanResult Fail(string message) => new(null, message);
}
