using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class QuantAnalysisEngineTests
{
    [Fact]
    public void Analyze_CalculatesYieldPeAndFcfYieldFromRawData()
    {
        var engine = new QuantAnalysisEngine();

        var result = engine.Analyze(new QuantAnalysisInput(
            "TEST",
            100m,
            new[]
            {
                new AnnualFundamentalPoint(2021, 5m, 400m, 500m, 100),
                new AnnualFundamentalPoint(2022, 5.5m, 450m, 550m, 100),
                new AnnualFundamentalPoint(2023, 6m, 500m, 600m, 100),
                new AnnualFundamentalPoint(2024, 6.5m, 550m, 650m, 100),
                new AnnualFundamentalPoint(2025, 8m, 600m, 800m, 100)
            },
            new[]
            {
                new AnnualDividendPoint(2021, 2m),
                new AnnualDividendPoint(2022, 2m),
                new AnnualDividendPoint(2023, 2.5m),
                new AnnualDividendPoint(2024, 3m),
                new AnnualDividendPoint(2025, 4m)
            },
            new[]
            {
                new AnnualPricePoint(2021, 80m),
                new AnnualPricePoint(2022, 90m),
                new AnnualPricePoint(2023, 100m),
                new AnnualPricePoint(2024, 110m),
                new AnnualPricePoint(2025, 120m)
            },
            false));

        Assert.Equal(4m, result.CurrentDividendYieldPercent);
        Assert.Equal(2.5m, result.HistoricalMedianDividendYieldPercent);
        Assert.Equal(12.5m, result.CurrentPe);
        Assert.Equal(15m, result.HistoricalMedianPe);
        Assert.Equal(6m, result.CurrentFcfYieldPercent);
        Assert.Equal(5.0m, result.HistoricalMedianFcfYieldPercent);
        Assert.Equal(50m, result.Metrics.PayoutRatio);
        Assert.Equal(66.6666666667m, Math.Round(result.Metrics.FcfPayoutRatio!.Value, 10));
    }

    [Fact]
    public void Analyze_PenalizesCyclicalHighDebtBusiness()
    {
        var engine = new QuantAnalysisEngine();

        var result = engine.Analyze(new QuantAnalysisInput(
            "TEST",
            100m,
            new[]
            {
                new AnnualFundamentalPoint(2021, 5m, 100m, 200m, 100),
                new AnnualFundamentalPoint(2022, 5m, 100m, 200m, 100),
                new AnnualFundamentalPoint(2023, 5m, 100m, 200m, 100),
                new AnnualFundamentalPoint(2024, 5m, 100m, 200m, 100),
                new AnnualFundamentalPoint(2025, 5m, 100m, 200m, 1000)
            },
            new[] { new AnnualDividendPoint(2025, 2m) },
            new[]
            {
                new AnnualPricePoint(2021, 100m),
                new AnnualPricePoint(2022, 100m),
                new AnnualPricePoint(2023, 100m),
                new AnnualPricePoint(2024, 100m),
                new AnnualPricePoint(2025, 100m)
            },
            true));

        Assert.Equal(9m, result.RiskScore);
    }
}
