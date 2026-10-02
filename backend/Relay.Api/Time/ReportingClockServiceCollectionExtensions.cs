using Relay.Api.Data;

namespace Relay.Api.Time;

public static class ReportingClockServiceCollectionExtensions
{
    /// <summary>Key of the reporting clock (D1). Resolve with <c>[FromKeyedServices(Key)]</c>.</summary>
    public const string Key = "reporting";

    /// <summary>
    /// Registers the D1 clock (fixed for the app's lifetime) as a keyed <see cref="TimeProvider"/>, so only
    /// reporting code sees the frozen "now". A plain <see cref="TimeProvider"/> (used by auth, caching,
    /// HTTP logging) stays the system clock.
    /// </summary>
    public static IServiceCollection AddReportingClock(this IServiceCollection services) =>
        services.AddKeyedSingleton<TimeProvider>(Key, (sp, _) =>
            ClockFactory.Create(
                sp.GetRequiredService<IConfiguration>()[ClockFactory.ConfigKey],
                () =>
                {
                    // Only queried when Clock:NowUtc is not set.
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<RelayDbContext>();
                    return db.ActivityEvents.Max(e => (DateTime?)e.OccurredAt);
                }));
}
