namespace Relay.Api.Reporting;

/// <summary>D23: every status threshold, bound from the <c>StatusRules</c> config section. Defaults are D6/D7/D21/D3.</summary>
public sealed record StatusThresholds
{
    public const string SectionName = "StatusRules";

    /// <summary>D3: complete weeks before the reported week that form the baseline.</summary>
    public int BaselineWeeks { get; init; } = 8;
    /// <summary>D6: the absolute part of the minimum gap, max(MinGap, RelativeGap·median).</summary>
    public double MinGap { get; init; } = 3;
    /// <summary>D6: the relative part of the minimum gap.</summary>
    public double RelativeGap { get; init; } = 0.3;
    /// <summary>D6: a median below this is Low volume.</summary>
    public double LowVolumeMedian { get; init; } = 5;
    /// <summary>D7/D21: a week needs at least this many known-outcome calls to have a rate.</summary>
    public int MinKnownCalls { get; init; } = 20;
    /// <summary>D7: minimum distance from the median, in percentage points, for a rate flag.</summary>
    public double RateGapPp { get; init; } = 10;
    /// <summary>D21: qualifying baseline weeks needed for a rate status.</summary>
    public int MinRateBaselineWeeks { get; init; } = 5;
}
