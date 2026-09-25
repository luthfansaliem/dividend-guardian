using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class FairValueEngineTests
{
    [Fact]
    public void DividendYieldFairValue_UsesForwardDpsAndTargetYield()
    {
        var result = FairValueEngine.DividendYieldFairValue(10m, 5m);

        Assert.Equal(200m, result);
    }

    [Fact]
    public void PeFairValue_UsesNormalizedEpsAndTargetPe()
    {
        var result = FairValueEngine.PeFairValue(12m, 10m);

        Assert.Equal(120m, result);
    }

    [Fact]
    public void FcfFairValue_UsesNormalizedFcfPerShareAndTargetYield()
    {
        var result = FairValueEngine.FcfFairValue(8m, 5m);

        Assert.Equal(160m, result);
    }

    [Fact]
    public void Calculate_UsesMedianOfAvailableMethodsAndBuildsRange()
    {
        var engine = new FairValueEngine();

        var result = engine.Calculate(new FairValueInput(
            ForwardDps: 10m,
            NormalizedEps: 12m,
            NormalizedFcfPerShare: 8m,
            HistoricalMedianDividendYieldPercent: 5m,
            HistoricalMedianPe: 10m,
            HistoricalMedianFcfYieldPercent: 5m));

        // Method values: 200, 120, 160. Median = 160.
        // Default range multipliers are 0.90x and 1.10x.
        Assert.Equal(144m, result.Range.Conservative);
        Assert.Equal(160m, result.Range.Base);
        Assert.Equal(176m, result.Range.Optimistic);
    }

    [Fact]
    public void Calculate_ReturnsUnavailableWhenNoMethodHasEnoughData()
    {
        var result = new FairValueEngine().Calculate(
            new FairValueInput(null, null, null, null, null, null));

        Assert.Null(result.Range.Conservative);
        Assert.Null(result.Range.Base);
        Assert.Null(result.Range.Optimistic);
        Assert.Contains(result.Reasons, x => x.Contains("unavailable"));
    }

    [Fact]
    public void MarginOfSafety_UsesConservativeFairValue()
    {
        var result = FairValueEngine.MarginOfSafety(90m, 120m);

        Assert.Equal(0.25m, result);
    }

    [Fact]
    public void MarginOfSafety_ReturnsNullForInvalidInputs()
    {
        Assert.Null(FairValueEngine.MarginOfSafety(0m, 120m));
        Assert.Null(FairValueEngine.MarginOfSafety(90m, null));
    }
}
