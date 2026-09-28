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
    public void DividendStability_Asii_IsOneBecauseLatestDpsDeclined()
    {
        var dividends = new[]
        {
            new AnnualDividendPoint(2019, 157),
            new AnnualDividendPoint(2020, 114),
            new AnnualDividendPoint(2021, 239),
            new AnnualDividendPoint(2022, 640),
            new AnnualDividendPoint(2023, 519),
            new AnnualDividendPoint(2024, 406),
            new AnnualDividendPoint(2025, 390)
        };

        var result = FundamentalMetricsEngine.CalculateStableOrGrowingYears(dividends);

        // The metric is a current consecutive non-declining streak, not the number
        // of historically stable/growing dividend years. Because 2025 DPS (390)
        // declined from 2024 DPS (406), the current streak is exactly one year.
        Assert.Equal(1, result);
    }

    [Fact]
    public void DividendCagr_Asii_IsPositiveEvenThoughCurrentStreakIsOne()
    {
        var dividends = new[]
        {
            new AnnualDividendPoint(2019, 157),
            new AnnualDividendPoint(2020, 114),
            new AnnualDividendPoint(2021, 239),
            new AnnualDividendPoint(2022, 640),
            new AnnualDividendPoint(2023, 519),
            new AnnualDividendPoint(2024, 406),
            new AnnualDividendPoint(2025, 390)
        };

        var engine = new FundamentalMetricsEngine();
        var fundamentals = new[]
        {
            new AnnualFundamentalPoint(2020, 1, 1, 1, 1),
            new AnnualFundamentalPoint(2025, 1, 1, 1, 1)
        };

        var result = engine.Calculate(fundamentals, dividends);

        Assert.NotNull(result.DividendCagr5Y);
        Assert.InRange(result.DividendCagr5Y!.Value, 27m, 29m);
        Assert.Equal(1, result.StableOrGrowingYears);
    }

    [Fact]
    public void Metrics_CalculatePayoutAndFcfPayout()
    {
        var engine = new FundamentalMetricsEngine();
        var fundamentals = new[]
        {
            new AnnualFundamentalPoint(2020, 7, 700, 900, 100),
            new AnnualFundamentalPoint(2021, 8, 800, 1000, 100),
            new AnnualFundamentalPoint(2022, 9, 900, 1100, 100),
            new AnnualFundamentalPoint(2023, 10, 1000, 1200, 100),
            new AnnualFundamentalPoint(2024, 11, 1100, 1300, 100),
            new AnnualFundamentalPoint(2025, 12, 1200, 1400, 100)
        };
        var dividends = new[] { new AnnualDividendPoint(2025, 3) };

        var result = engine.Calculate(fundamentals, dividends);

        Assert.Equal(21.428571428571428571428571430m, result.PayoutRatio);
        Assert.Equal(25m, result.FcfPayoutRatio);
        Assert.NotNull(result.EpsCagr5Y);
        Assert.True(result.EpsCagr5Y > 8m);
    }
}
