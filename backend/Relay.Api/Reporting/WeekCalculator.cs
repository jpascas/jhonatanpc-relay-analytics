namespace Relay.Api.Reporting;

/// <summary>A local Mon 00:00 → Mon 00:00 week as a half-open UTC range <c>[StartUtc, EndUtc)</c>.</summary>
public readonly record struct WeekRange(DateOnly LocalStart, DateTime StartUtc, DateTime EndUtc);

/// <summary>The reported week and its baseline weeks, oldest first.</summary>
public sealed record ReportingWeeks(WeekRange Reported, IReadOnlyList<WeekRange> Baseline);

/// <summary>
/// D2/D15/D29: week edges are computed here from the account's IANA zone, so SQL only filters on UTC ranges.
/// Each edge is converted separately, so a week containing a DST change is 167 or 169 hours long (G-08).
/// </summary>
public static class WeekCalculator
{
    public static WeekRange Week(DateOnly localMonday, TimeZoneInfo zone) =>
        new(localMonday, LocalMidnightToUtc(localMonday, zone), LocalMidnightToUtc(localMonday.AddDays(7), zone));

    /// <summary>D2: the last local week that has fully ended at <paramref name="now"/>.</summary>
    public static DateOnly LastCompleteWeek(DateTimeOffset now, TimeZoneInfo zone) =>
        WeekContaining(now, zone).AddDays(-7);

    /// <summary>The local Monday of the week that contains <paramref name="instant"/>.</summary>
    public static DateOnly WeekContaining(DateTimeOffset instant, TimeZoneInfo zone)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
        return localDate.AddDays(-DaysSinceMonday(localDate));
    }

    /// <summary>
    /// The reported week (<paramref name="requestedWeek"/>, or the default D2 week) and the
    /// <paramref name="baselineWeeks"/> weeks before it (D3). Bounds of a requested week are checked by the caller (D29).
    /// </summary>
    public static ReportingWeeks ForReport(TimeZoneInfo zone, DateTimeOffset now, DateOnly? requestedWeek, int baselineWeeks)
    {
        if (requestedWeek is { } requested && requested.DayOfWeek != DayOfWeek.Monday)
        {
            throw new ArgumentException($"Week {requested:yyyy-MM-dd} is not a Monday.", nameof(requestedWeek));
        }

        var reported = requestedWeek ?? LastCompleteWeek(now, zone);
        var baseline = Enumerable.Range(1, baselineWeeks)
            .Select(i => Week(reported.AddDays(-7 * (baselineWeeks - i + 1)), zone))
            .ToList();
        return new ReportingWeeks(Week(reported, zone), baseline);
    }

    private static int DaysSinceMonday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    // Midnight is never inside a DST gap in the seed's zones (E-05: US zones change at 02:00).
    private static DateTime LocalMidnightToUtc(DateOnly date, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
}
