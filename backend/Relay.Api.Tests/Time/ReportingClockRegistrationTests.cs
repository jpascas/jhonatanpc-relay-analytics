using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Relay.Api.Time;

namespace Relay.Api.Tests.Time;

/// <summary>
/// The frozen D1 clock is a keyed service, so framework code that resolves a plain
/// <see cref="TimeProvider"/> (auth, caching, HTTP logging) never sees it.
/// </summary>
public class ReportingClockRegistrationTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection>? extra = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Clock:NowUtc"] = "2026-07-27T22:20:34Z" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        extra?.Invoke(services);
        services.AddReportingClock();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Reporting_clock_is_resolved_by_key()
    {
        using var provider = BuildProvider();

        var clock = provider.GetRequiredKeyedService<TimeProvider>(ReportingClockServiceCollectionExtensions.Key);

        Assert.Equal(new DateTimeOffset(2026, 7, 27, 22, 20, 34, TimeSpan.Zero), clock.GetUtcNow());
    }

    [Fact]
    public void Plain_TimeProvider_is_not_the_reporting_clock()
    {
        using var provider = BuildProvider();

        Assert.Null(provider.GetService<TimeProvider>());
    }

    [Fact]
    public void Framework_registration_of_the_system_clock_is_left_alone()
    {
        // What AddAuthentication and similar framework calls do.
        using var provider = BuildProvider(s => s.AddSingleton(TimeProvider.System));

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }
}
