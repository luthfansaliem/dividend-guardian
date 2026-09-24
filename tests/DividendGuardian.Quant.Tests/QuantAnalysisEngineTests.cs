using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class QuantAnalysisEngineTests
{
    [Fact]
    public void Analyze_ComputesYieldPeAndFcfYieldAgainstHistoricalBaselines()
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
        Assert.Equal(12m / 1m, result.HistoricalMedianPe);
        Assert.Equal(12m, result.CurrentFcfYieldPercent);
        Assert.NotNull(result.HistoricalMedianFcfYieldPercent);
        Assert.Contains(result.Reasons, x => x.Contains("Dividend yield 3.00%"));
        Assert.Contains(result.Reasons, x => x.Contains("PE 8.33x"));
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

        Assert.Equal(first.Ticker, second.Ticker);
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
        Assert.Equal(first.Reasons, second.Reasons);
    }

    [Fact]
    public void Analyze_DoesNotInventMissingHistoricalBaselines()
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
    }
}
