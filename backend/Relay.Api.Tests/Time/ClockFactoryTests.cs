using Relay.Api.Time;

namespace Relay.Api.Tests.Time;

public class ClockFactoryTests
{
    private static readonly DateTimeOffset DataNow = new(2026, 7, 27, 22, 20, 34, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-07-27T22:20:34Z")]
    [InlineData("2026-07-27 22:20:34")] // no offset: read as UTC, never as machine-local time
    public void Configured_now_wins_and_the_data_is_not_queried(string configured)
    {
        var clock = ClockFactory.Create(configured, () => throw new Xunit.Sdk.XunitException("data was queried"));

        Assert.Equal(DataNow, clock.GetUtcNow());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Without_config_now_is_the_latest_occurred_at(string? configured)
    {
        // EF reads datetime2 as DateTimeKind.Unspecified; the column holds UTC.
        var clock = ClockFactory.Create(configured, () => new DateTime(2026, 7, 27, 22, 20, 34, DateTimeKind.Unspecified));

        Assert.Equal(DataNow, clock.GetUtcNow());
    }

    [Fact]
    public void Without_config_or_events_the_system_clock_is_used()
    {
        var clock = ClockFactory.Create(null, () => null);

        Assert.Same(TimeProvider.System, clock);
    }

    [Fact]
    public void Invalid_config_value_names_the_setting()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ClockFactory.Create("not-a-date", () => null));

        Assert.Contains("Clock:NowUtc", ex.Message);
    }
}
