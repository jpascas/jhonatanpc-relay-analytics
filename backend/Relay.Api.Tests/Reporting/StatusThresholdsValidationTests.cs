using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Relay.Api.Reporting;

namespace Relay.Api.Tests.Reporting;

public class StatusThresholdsValidationTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddStatusThresholds(configuration).BuildServiceProvider();
    }

    [Fact] // D23 defaults
    public void Defaults_are_the_plan_values()
    {
        using var provider = BuildProvider([]);

        Assert.Equal(
            new StatusThresholds
            {
                BaselineWeeks = 8, MinGap = 3, RelativeGap = 0.3, LowVolumeMedian = 5,
                MinKnownCalls = 20, RateGapPp = 10, MinRateBaselineWeeks = 5,
            },
            provider.GetRequiredService<IOptions<StatusThresholds>>().Value);
    }

    [Fact]
    public void Values_are_bound_from_the_StatusRules_section()
    {
        using var provider = BuildProvider(new() { ["StatusRules:MinGap"] = "4" });

        Assert.Equal(4, provider.GetRequiredService<IOptions<StatusThresholds>>().Value.MinGap);
    }

    [Fact] // S4 "Done when": relative gap 1.5 fails, naming the setting
    public void Relative_gap_1_5_fails_naming_the_setting()
    {
        using var provider = BuildProvider(new() { ["StatusRules:RelativeGap"] = "1.5" });

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<StatusThresholds>>().Value);
        Assert.Contains("StatusRules:RelativeGap", ex.Message);
    }

    [Theory] // §5 Bad config, plus the other ranges
    [InlineData("BaselineWeeks", "3", "StatusRules:BaselineWeeks")]
    [InlineData("RelativeGap", "-0.1", "StatusRules:RelativeGap")]
    [InlineData("MinGap", "-1", "StatusRules:MinGap")]
    [InlineData("LowVolumeMedian", "-1", "StatusRules:LowVolumeMedian")]
    [InlineData("MinKnownCalls", "0", "StatusRules:MinKnownCalls")]
    [InlineData("RateGapPp", "101", "StatusRules:RateGapPp")]
    [InlineData("MinRateBaselineWeeks", "0", "StatusRules:MinRateBaselineWeeks")]
    [InlineData("MinRateBaselineWeeks", "9", "StatusRules:MinRateBaselineWeeks")] // more than BaselineWeeks (8)
    public void Out_of_range_values_name_the_setting(string key, string value, string expectedName)
    {
        var result = new StatusThresholdsValidator().Validate(
            null, Bind(new() { [$"StatusRules:{key}"] = value }));

        Assert.True(result.Failed, $"{key}={value} should fail");
        Assert.Contains(expectedName, result.FailureMessage);
    }

    [Fact]
    public void Defaults_are_valid()
    {
        Assert.True(new StatusThresholdsValidator().Validate(null, new StatusThresholds()).Succeeded);
    }

    private static StatusThresholds Bind(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build()
            .GetSection(StatusThresholds.SectionName).Get<StatusThresholds>() ?? new StatusThresholds();
}
