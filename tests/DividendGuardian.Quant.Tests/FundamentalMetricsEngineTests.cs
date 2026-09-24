using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public class FundamentalMetricsEngineTests
{
    [Fact]
    public void Cagr_CalculatesFiveYearGrowth()
    {
        var result = FundamentalMetricsEngine.Cagr(100m, 161.051m, 5);
        Assert.NotNull(result);
        Assert.InRange(result.Value, 9.99m, 10.01m);
    }

    [Fact]
    public void DividendStability_CountsOnlyCurrentConsecutiveNonDecliningYears()
    {
        var dividends = new[]
        {
            new AnnualDividendPoint(2021, 50),
            new AnnualDividendPoint(2022, 55),
            new AnnualDividendPoint(2023, 60),
            new AnnualDividendPoint(2024, 58),
            new AnnualDividendPoint(2025, 62)
        };

        Assert.Equal(2, FundamentalMetricsEngine.CalculateStableOrGrowingYears(dividends));
    }

    [Fact]
    public void Metrics_CalculatePayoutAndFcfPayout()
    {
        var engine = new FundamentalMetricsEngine();
        var fundamentals = new[]
        {
            new AnnualFundamentalPoint(2021, 8, 800, 1000, 100),
            new AnnualFundamentalPoint(2022, 9, 900, 1100, 100),
            new AnnualFundamentalPoint(2023, 10, 1000, 1200, 100),
            new AnnualFundamentalPoint(2024, 11, 1100, 1300, 100),
            new AnnualFundamentalPoint(2025, 12, 1200, 1400, 100)
        };
        var dividends = new[] { new AnnualDividendPoint(2025, 3) };

        var result = engine.Calculate(fundamentals, dividends);

        Assert.Equal(21.4285714285714285714285714286m, result.PayoutRatio);
        Assert.Equal(25m, result.FcfPayoutRatio);
        Assert.NotNull(result.EpsCagr5Y);
        Assert.True(result.EpsCagr5Y > 8m);
    }
}
