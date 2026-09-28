using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class NormalizedValuationTests
{
    [Fact]
    public void Calculate_UsesLatestThreeSharedFiscalYearsAndMedians()
    {
        var fundamentals = new[]
        {
            new AnnualFundamentalPoint(2022, 715m, 600m, 1_000m, 100),
            new AnnualFundamentalPoint(2023, 836m, 700m, 1_100m, 100),
            new AnnualFundamentalPoint(2024, 837m, 900m, 1_200m, 100),
            new AnnualFundamentalPoint(2025, 810m, 800m, 1_300m, 100)
        };

        var dividends = new[]
        {
            new AnnualDividendPoint(2020, 114m),
            new AnnualDividendPoint(2021, 239m),
            new AnnualDividendPoint(2022, 640m),
            new AnnualDividendPoint(2023, 519m),
            new AnnualDividendPoint(2024, 406m),
            new AnnualDividendPoint(2025, 390m)
        };

        var result = NormalizedValuationEngine.Calculate(fundamentals, dividends);

        // Shared latest window is 2023-2025.
        Assert.Equal(3, result.YearCount);
        Assert.Equal(2023, result.StartYear);
        Assert.Equal(2025, result.EndYear);

        // DPS = 519, 406, 390 -> median 406.
        Assert.Equal(406m, result.MedianDps);

        // EPS = 836, 837, 810 -> median 836.
        Assert.Equal(836m, result.MedianEps);

        // FCF/share = 7, 9, 8 -> median 8.
        Assert.Equal(8m, result.MedianFcfPerShare);
    }

    [Fact]
    public void Calculate_DoesNotLetThe2022DividendSpikeDriveTheThreeYearMedian()
    {
        var fundamentals = new[]
        {
            new AnnualFundamentalPoint(2022, 715m, 600m, 1_000m, 100),
            new AnnualFundamentalPoint(2023, 836m, 700m, 1_100m, 100),
            new AnnualFundamentalPoint(2024, 837m, 900m, 1_200m, 100),
            new AnnualFundamentalPoint(2025, 810m, 800m, 1_300m, 100)
        };

        var dividends = new[]
        {
            new AnnualDividendPoint(2022, 640m),
            new AnnualDividendPoint(2023, 519m),
            new AnnualDividendPoint(2024, 406m),
            new AnnualDividendPoint(2025, 390m)
        };

        var result = NormalizedValuationEngine.Calculate(fundamentals, dividends);

        Assert.Equal(406m, result.MedianDps);
        Assert.NotEqual(640m, result.MedianDps);
    }

    [Fact]
    public void Calculate_ReturnsEmptyWhenThereAreNoSharedYears()
    {
        var fundamentals = new[]
        {
            new AnnualFundamentalPoint(2025, 810m, 800m, 1_300m, 100)
        };

        var dividends = new[]
        {
            new AnnualDividendPoint(2024, 406m)
        };

        var result = NormalizedValuationEngine.Calculate(fundamentals, dividends);

        Assert.Equal(0, result.YearCount);
        Assert.Null(result.MedianDps);
        Assert.Null(result.MedianEps);
        Assert.Null(result.MedianFcfPerShare);
    }
}
