namespace Relay.Api.Time;

/// <summary>A clock pinned to one instant (D1).</summary>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
