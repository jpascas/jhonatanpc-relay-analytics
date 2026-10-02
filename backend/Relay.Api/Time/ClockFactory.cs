using System.Globalization;

namespace Relay.Api.Time;

/// <summary>
/// D1: "now" is <c>Clock:NowUtc</c> when set, otherwise the latest <c>occurred_at</c> in the data,
/// otherwise (no events at all) the system clock.
/// </summary>
public static class ClockFactory
{
    public const string ConfigKey = "Clock:NowUtc";

    public static TimeProvider Create(string? configuredNowUtc, Func<DateTime?> latestOccurredAtUtc)
    {
        if (!string.IsNullOrWhiteSpace(configuredNowUtc))
        {
            // A value without an offset is UTC, never the machine's local time.
            if (!DateTimeOffset.TryParse(configuredNowUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var configured))
            {
                throw new InvalidOperationException(
                    $"'{ConfigKey}' value '{configuredNowUtc}' is not a valid date and time (e.g. 2026-07-27T22:20:34Z).");
            }
            return new FixedTimeProvider(configured.ToUniversalTime());
        }

        // occurred_at is stored as UTC; EF reads it with DateTimeKind.Unspecified.
        return latestOccurredAtUtc() is { } latest
            ? new FixedTimeProvider(new DateTimeOffset(DateTime.SpecifyKind(latest, DateTimeKind.Utc)))
            : TimeProvider.System;
    }
}
