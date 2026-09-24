using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class QuantAnalysisEngineTests
{
    [Fact]
    public void Analyze_ComputesYieldPeFcfAndFairValueAgainstHistoricalBaselines()
    {
        var engine = new QuantAnalysisEngine();
        var fundamentals = new[]
        {
            new AnnualFundamentalPoint(2021, 8m, 800m, 1000m, 100),
            new AnnualFundamentalPoint(2022, 9m, 900m, 1100m, 100),
            new AnnualFundamentalPoint(2023, 10m, 1000m, 1200m, 100),
            new AnnualFundamentalPoint(2024, 11m, 1100m, 1300m, 100),
            new AnnualFundamentalPoint(2025, 12m, 1200m, 1400m, 100)
        };
        var dividends = new[]
        {
            new AnnualDividendPoint(2021, 2m),
            new AnnualDividendPoint(2022, 2.2m),
            new AnnualDividendPoint(2023, 2.4m),
            new AnnualDividendPoint(2024, 2.6m),
            new AnnualDividendPoint(2025, 3m)
        };
        var prices = new[]
        {
            new AnnualPricePoint(2021, 100m),
            new AnnualPricePoint(2022, 110m),
            new AnnualPricePoint(2023, 120m),
            new AnnualPricePoint(2024, 130m),
            new AnnualPricePoint(2025, 150m)
        };

        var result = engine.Analyze(new QuantAnalysisInput(
            "TEST", 100m, fundamentals, dividends, prices, false));

        Assert.Equal(3m, result.CurrentDividendYieldPercent);
        Assert.Equal(2m, result.HistoricalMedianDividendYieldPercent);
        Assert.Equal(100m / 12m, result.CurrentPe);
        Assert.Equal(110m / 9m, result.HistoricalMedianPe);
        Assert.Equal(12m, result.CurrentFcfYieldPercent);
        Assert.NotNull(result.HistoricalMedianFcfYieldPercent);

        // Dividend fair value = 150.
        // PE fair value = 12 * (110 / 9) = 146.666...
        // FCF fair value converges to the same value with this fixture.
        // Median base = 146.666...; default range = 132-161.333...
        Assert.True(Math.Abs(result.FairValue.Conservative!.Value - 132m) < 0.00000001m);
        Assert.True(Math.Abs(result.FairValue.Base!.Value - 146.6666666666666666666666666666667m) < 0.00000001m);
        Assert.True(Math.Abs(result.FairValue.Optimistic!.Value - 161.3333333333333333333333333333333m) < 0.00000001m);
        Assert.True(Math.Abs(result.MarginOfSafety!.Value - (1m - 100m / 132m)) < 0.00000001m);
        Assert.Equal("STRONG_ACCUMULATE", result.BuyZone.Status);
        Assert.Equal("READY", result.DataQuality);
        Assert.Equal(result.MarginOfSafety, result.BuyZone.MarginOfSafety);

        Assert.Contains(result.Reasons, x => x.Contains("Fair value base 146.67"));
        Assert.Contains(result.Reasons, x => x.Contains("Margin of safety"));
    }

    [Fact]
    public void Analyze_IsDeterministicForSameInput()
    {
        var input = new QuantAnalysisInput(
            "TEST",
            100m,
            new[]
            {
                new AnnualFundamentalPoint(2024, 10m, 900m, 1000m, 100),
                new AnnualFundamentalPoint(2025, 12m, 1200m, 1400m, 100)
            },
            new[]
            {
                new AnnualDividendPoint(2024, 2m),
                new AnnualDividendPoint(2025, 3m)
            },
            new[]
            {
                new AnnualPricePoint(2024, 120m),
                new AnnualPricePoint(2025, 150m)
            },
            false);

        var first = new QuantAnalysisEngine().Analyze(input);
        var second = new QuantAnalysisEngine().Analyze(input);

        Assert.Equal(first.CurrentPrice, second.CurrentPrice);
        Assert.Equal(first.CurrentDividendYieldPercent, second.CurrentDividendYieldPercent);
        Assert.Equal(first.HistoricalMedianDividendYieldPercent, second.HistoricalMedianDividendYieldPercent);
        Assert.Equal(first.CurrentPe, second.CurrentPe);
        Assert.Equal(first.HistoricalMedianPe, second.HistoricalMedianPe);
        Assert.Equal(first.CurrentFcfYieldPercent, second.CurrentFcfYieldPercent);
        Assert.Equal(first.HistoricalMedianFcfYieldPercent, second.HistoricalMedianFcfYieldPercent);
        Assert.Equal(first.RiskScore, second.RiskScore);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Metrics, second.Metrics);
        Assert.Equal(first.FairValue, second.FairValue);
        Assert.Equal(first.MarginOfSafety, second.MarginOfSafety);
        Assert.Equal(first.BuyZone, second.BuyZone);
        Assert.Equal(first.Reasons, second.Reasons);
    }

    [Fact]
    public void Analyze_DoesNotInventMissingHistoricalBaselinesOrFairValue()
    {
        var input = new QuantAnalysisInput(
            "TEST",
            100m,
            new[]
            {
                new AnnualFundamentalPoint(2025, 10m, 900m, 1000m, 100)
            },
            new[]
            {
                new AnnualDividendPoint(2025, 3m)
            },
            Array.Empty<AnnualPricePoint>(),
            false);

        var result = new QuantAnalysisEngine().Analyze(input);

        Assert.Null(result.HistoricalMedianDividendYieldPercent);
        Assert.Null(result.HistoricalMedianPe);
        Assert.Null(result.HistoricalMedianFcfYieldPercent);
        Assert.Equal(3m, result.CurrentDividendYieldPercent);
        Assert.Null(result.FairValue.Conservative);
        Assert.Null(result.FairValue.Base);
        Assert.Null(result.FairValue.Optimistic);
        Assert.Null(result.MarginOfSafety);
        Assert.Equal("LIMITED", result.DataQuality);
    }

    [Fact]
    public void Analyze_ClassifiesPartialQuality_WhenHistoryExistsButOneValuationBaselineIsMissing()
    {
        var input = new QuantAnalysisInput(
            "TEST", 100m,
            new[]
            {
                new AnnualFundamentalPoint(2023, 8m, 800m, 1000m, 100),
                new AnnualFundamentalPoint(2024, 9m, 900m, 1100m, 100),
                new AnnualFundamentalPoint(2025, 10m, 1000m, 1200m, 100)
            },
            new[]
            {
                new AnnualDividendPoint(2023, 2m),
                new AnnualDividendPoint(2024, 2m),
                new AnnualDividendPoint(2025, 3m)
            },
            new[]
            {
                new AnnualPricePoint(2023, 100m),
                new AnnualPricePoint(2024, 110m),
                new AnnualPricePoint(2025, 120m)
            },
            false);

        var result = new QuantAnalysisEngine().Analyze(input);

        Assert.Equal("READY", result.DataQuality);
    }
}
