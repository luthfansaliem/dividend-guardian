using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class BuyZoneEngineTests
{
    [Fact]
    public void Evaluate_ReturnsStrongAccumulate_WhenMosAndQuantScoreMeetThresholds()
    {
        var result = new BuyZoneEngine().Evaluate(80m, 100m, 110m, 120m, 85m);

        Assert.Equal("STRONG_ACCUMULATE", result.Status);
        Assert.Equal(0.20m, result.MarginOfSafety);
        Assert.Equal(5, result.Reasons.Count);
    }

    [Fact]
    public void Evaluate_ReturnsAccumulate_WhenModerateMosAndQuantScoreMeetThresholds()
    {
        var result = new BuyZoneEngine().Evaluate(90m, 100m, 110m, 120m, 75m);

        Assert.Equal("ACCUMULATE", result.Status);
        Assert.Equal(0.10m, result.MarginOfSafety);
    }

    [Fact]
    public void Evaluate_ReturnsWatch_WhenPriceIsNotAboveConservativeFairValueButThresholdsAreNotMet()
    {
        var result = new BuyZoneEngine().Evaluate(95m, 100m, 110m, 120m, 60m);

        Assert.Equal("WATCH", result.Status);
        Assert.Equal(0.05m, result.MarginOfSafety);
    }

    [Fact]
    public void Evaluate_ReturnsReview_WhenPriceIsAboveConservativeFairValue()
    {
        var result = new BuyZoneEngine().Evaluate(110m, 100m, 120m, 140m, 95m);

        Assert.Equal("REVIEW", result.Status);
        Assert.Equal(-0.10m, result.MarginOfSafety);
    }

    [Fact]
    public void Evaluate_ReturnsReview_WhenFairValueIsMissing()
    {
        var result = new BuyZoneEngine().Evaluate(100m, null, 120m, 140m, 90m);

        Assert.Equal("REVIEW", result.Status);
        Assert.Null(result.MarginOfSafety);
        Assert.Contains(result.Reasons, x => x.Contains("unavailable"));
    }

    [Fact]
    public void Evaluate_IsDeterministic()
    {
        var engine = new BuyZoneEngine();

        var first = engine.Evaluate(82m, 100m, 110m, 120m, 82m);
        var second = engine.Evaluate(82m, 100m, 110m, 120m, 82m);

        Assert.Equal(first.ConservativeFairValue, second.ConservativeFairValue);
        Assert.Equal(first.BaseFairValue, second.BaseFairValue);
        Assert.Equal(first.OptimisticFairValue, second.OptimisticFairValue);
        Assert.Equal(first.MarginOfSafety, second.MarginOfSafety);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Reasons, second.Reasons);
    }
}
