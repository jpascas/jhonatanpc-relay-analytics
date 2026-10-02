using Microsoft.Extensions.Options;

namespace Relay.Api.Reporting;

/// <summary>D23: reject threshold values that make the rules meaningless; failures name the config key.</summary>
public sealed class StatusThresholdsValidator : IValidateOptions<StatusThresholds>
{
    private const string Prefix = StatusThresholds.SectionName + ":";

    public ValidateOptionsResult Validate(string? name, StatusThresholds o)
    {
        var failures = new List<string>();

        // At least 4 weeks so P25/P75 do not collapse onto the extremes.
        Check(o.BaselineWeeks >= 4, nameof(o.BaselineWeeks), o.BaselineWeeks, "must be at least 4");
        Check(o.MinGap >= 0, nameof(o.MinGap), o.MinGap, "must be 0 or more");
        Check(o.RelativeGap is >= 0 and <= 1, nameof(o.RelativeGap), o.RelativeGap, "must be between 0 and 1");
        Check(o.LowVolumeMedian >= 0, nameof(o.LowVolumeMedian), o.LowVolumeMedian, "must be 0 or more");
        Check(o.MinKnownCalls >= 1, nameof(o.MinKnownCalls), o.MinKnownCalls, "must be at least 1");
        Check(o.RateGapPp is >= 0 and <= 100, nameof(o.RateGapPp), o.RateGapPp, "must be between 0 and 100");
        Check(o.MinRateBaselineWeeks >= 1 && o.MinRateBaselineWeeks <= o.BaselineWeeks,
            nameof(o.MinRateBaselineWeeks), o.MinRateBaselineWeeks,
            $"must be between 1 and {Prefix}{nameof(o.BaselineWeeks)} ({o.BaselineWeeks})");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);

        void Check(bool ok, string key, object value, string rule)
        {
            if (!ok)
            {
                failures.Add($"{Prefix}{key} {rule} (was {value}).");
            }
        }
    }
}

public static class StatusThresholdsServiceCollectionExtensions
{
    /// <summary>Binds <c>StatusRules</c>, validates it, and fails startup on invalid values (D23).</summary>
    public static IServiceCollection AddStatusThresholds(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<StatusThresholds>, StatusThresholdsValidator>();
        services.AddOptions<StatusThresholds>()
            .Bind(configuration.GetSection(StatusThresholds.SectionName))
            .ValidateOnStart();
        return services;
    }
}
