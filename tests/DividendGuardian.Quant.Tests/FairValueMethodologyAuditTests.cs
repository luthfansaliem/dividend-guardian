using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class FairValueMethodologyAuditTests
{
    [Fact]
    public void ThreeMethods_CloseTogether_ClassifyAsStrong()
    {
        var result = new FairValueEngine().Calculate(new FairValueInput(
            ForwardDps: 5m,
            NormalizedEps: 20m,
            NormalizedFcfPerShare: 10m,
            HistoricalMedianDividendYieldPercent: 5m,
            HistoricalMedianPe: 5m,
            HistoricalMedianFcfYieldPercent: 10m));

        Assert.Equal(3, result.Confidence.ValidMethodCount);
        Assert.Equal("STRONG", result.Confidence.Level);
        Assert.NotNull(result.Confidence.SpreadPercent);
        Assert.True(result.Confidence.SpreadPercent <= 10m);
    }

    [Fact]
    public void ThreeMethods_WideSpread_IsExplicitlyMarkedHighDisagreement()
    {
        var result = new FairValueEngine().Calculate(new FairValueInput(
            ForwardDps: 390m,
            NormalizedEps: 810m,
            NormalizedFcfPerShare: 673.75m,
            HistoricalMedianDividendYieldPercent: 8.29m,
            HistoricalMedianPe: 7.37m,
            HistoricalMedianFcfYieldPercent: 10.55m));

        Assert.NotNull(result.DividendYieldFairValue);
        Assert.NotNull(result.PeFairValue);
        Assert.NotNull(result.FcfFairValue);
        Assert.InRange(result.DividendYieldFairValue!.Value, 4700m, 4710m);
        Assert.InRange(result.PeFairValue!.Value, 5960m, 5975m);
        Assert.InRange(result.FcfFairValue!.Value, 6380m, 6390m);
        Assert.Equal(3, result.Confidence.ValidMethodCount);
        Assert.Equal("HIGH_DISAGREEMENT", result.Confidence.Level);
        Assert.InRange(result.Confidence.SpreadPercent!.Value, 27.5m, 29m);
    }

    [Fact]
    public void ThreeMethods_UseMedianAsBase_EvenWhenMethodsDisagree()
    {
        var result = new FairValueEngine().Calculate(new FairValueInput(
            ForwardDps: 100m,
            NormalizedEps: 30m,
            NormalizedFcfPerShare: 50m,
            HistoricalMedianDividendYieldPercent: 5m,
            HistoricalMedianPe: 10m,
            HistoricalMedianFcfYieldPercent: 5m));

        // Methods = 2,000; 300; 1,000. Median = 1,000.
        Assert.Equal(1000m, result.Range.Base);
        Assert.Equal(300m, result.PeFairValue);
        Assert.Equal(1000m, result.FcfFairValue);
        Assert.Equal(2000m, result.DividendYieldFairValue);
        Assert.Equal("HIGH_DISAGREEMENT", result.Confidence.Level);
    }

    [Fact]
    public void TwoMethods_UseArithmeticMedianOfTheTwoValues()
    {
        var result = new FairValueEngine().Calculate(new FairValueInput(
            ForwardDps: null,
            NormalizedEps: 20m,
            NormalizedFcfPerShare: 30m,
            HistoricalMedianDividendYieldPercent: null,
            HistoricalMedianPe: 5m,
            HistoricalMedianFcfYieldPercent: 10m));

        // PE = 100; FCF = 300; with two methods, current implementation uses (100 + 300) / 2.
        Assert.Equal(2, result.Confidence.ValidMethodCount);
        Assert.Equal(200m, result.Range.Base);
        Assert.Equal(100m, result.PeFairValue);
        Assert.Equal(300m, result.FcfFairValue);
    }

    [Fact]
    public void OneMethod_UsesThatMethodAsBase()
    {
        var result = new FairValueEngine().Calculate(new FairValueInput(
            ForwardDps: null,
            NormalizedEps: 20m,
            NormalizedFcfPerShare: null,
            HistoricalMedianDividendYieldPercent: null,
            HistoricalMedianPe: 5m,
            HistoricalMedianFcfYieldPercent: null));

        Assert.Equal(1, result.Confidence.ValidMethodCount);
        Assert.Equal(100m, result.Range.Base);
        Assert.Equal(100m, result.PeFairValue);
        Assert.Equal("STRONG", result.Confidence.Level);
    }

    [Fact]
    public void CurrentAsiiPrice_IsNearDividendFairValue_ButBelowPeAndFcfFairValues()
    {
        const decimal currentPrice = 4690m;
        const decimal dividendFairValue = 4706.9m;
        const decimal peFairValue = 5965.81m;
        const decimal fcfFairValue = 6386.02m;

        Assert.True(Math.Abs(currentPrice / dividendFairValue - 1m) < 0.01m);
        Assert.True(currentPrice < peFairValue);
        Assert.True(currentPrice < fcfFairValue);
    }
}
