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
        Assert.Equal(6, result.Reasons.Count);
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
    public void Evaluate_CapsStrongAccumulate_WhenValuationMethodsDisagreeHighly()
    {
        var result = new BuyZoneEngine().Evaluate(80m, 100m, 110m, 120m, 85m, "HIGH_DISAGREEMENT");

        Assert.Equal("ACCUMULATE", result.Status);
        Assert.Contains(result.Reasons, x => x.Contains("High valuation-method disagreement"));
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

    [Theory]
    [InlineData(80, 100, 110, 120, 85, "MODERATE", "STRONG_ACCUMULATE")]
    [InlineData(80, 100, 110, 120, 85, "HIGH_DISAGREEMENT", "ACCUMULATE")]
    [InlineData(80, 100, 110, 120, 85, "STRONG", "STRONG_ACCUMULATE")]
    public void Evaluate_ConfidenceOnlyAffectsStrongStatusCap(
        decimal currentPrice,
        decimal conservative,
        decimal baseValue,
        decimal optimistic,
        decimal quantScore,
        string confidence,
        string expectedStatus)
    {
        var result = new BuyZoneEngine().Evaluate(
            currentPrice, conservative, baseValue, optimistic, quantScore, confidence);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(0.20m, result.MarginOfSafety);
    }

    [Fact]
    public void Evaluate_HighDisagreement_DoesNotChangeMarginOfSafety()
    {
        var normal = new BuyZoneEngine().Evaluate(
            4690m, 5369.23m, 5965.81m, 6562.39m, 72.9m, "MODERATE");

        var disagreement = new BuyZoneEngine().Evaluate(
            4690m, 5369.23m, 5965.81m, 6562.39m, 72.9m, "HIGH_DISAGREEMENT");

        Assert.Equal(normal.MarginOfSafety, disagreement.MarginOfSafety);
        Assert.Equal(0.126504173m, Math.Round(disagreement.MarginOfSafety!.Value, 9));
        Assert.Equal("ACCUMULATE", disagreement.Status);
    }

    [Fact]
    public void Evaluate_AsiiScenario_HighDisagreementPreventsStrongAccumulation()
    {
        var result = new BuyZoneEngine().Evaluate(
            currentPrice: 4690m,
            conservative: 5369.23m,
            baseValue: 5965.81m,
            optimistic: 6562.39m,
            quantScore: 72.9m,
            valuationConfidenceLevel: "HIGH_DISAGREEMENT");

        Assert.Equal("ACCUMULATE", result.Status);
        Assert.InRange(result.MarginOfSafety!.Value, 0.1265m, 0.1268m);
        Assert.Contains(result.Reasons, x => x.Contains("disagree materially"));
    }
}
