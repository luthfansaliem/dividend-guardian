using DividendGuardian.Infrastructure;

namespace DividendGuardian.Quant.Tests;

public sealed class YahooFinanceFundamentalDataProviderTests
{
    [Fact]
    public async Task GetDividendsAsync_Asii_ReturnsKnownRecentDividends()
    {
        using var httpClient = new HttpClient();
        var provider = new YahooFinanceFundamentalDataProvider(httpClient);

        var from = new DateOnly(2025, 1, 1);
        var to = new DateOnly(2026, 12, 31);

        var dividends = await provider.GetDividendsAsync("ASII.JK", from, to);

        Assert.NotEmpty(dividends);

        var byYear = dividends.ToDictionary(x => x.FiscalYear);

        Assert.True(byYear.TryGetValue(2025, out var dividend2025));
        Assert.False(byYear.ContainsKey(2026));

        // Yahoo dividend events are attributed to the Indonesian fiscal year:
        // the May 2026 event belongs to FY2025 together with the 2025 interim dividend.
        Assert.Equal(390m, dividend2025!.Dps);

        Console.WriteLine($"ASII FY2025 DPS={dividend2025.Dps}");
    }

    [Fact]
    public async Task GetAnnualFundamentalsAsync_Asii_ReturnsRecentStatements()
    {
        using var httpClient = new HttpClient();
        var provider = new YahooFinanceFundamentalDataProvider(httpClient);

        var from = new DateOnly(2021, 1, 1);
        var to = new DateOnly(2026, 12, 31);

        var fundamentals = await provider.GetAnnualFundamentalsAsync("ASII.JK", from, to);

        Assert.NotEmpty(fundamentals);

        var latest = fundamentals.OrderByDescending(x => x.PeriodEnd).First();

        Assert.Equal("ASII.JK", latest.Ticker);
        Assert.True(latest.Revenue > 0);
        Assert.True(latest.NetIncome > 0);
        Assert.True(latest.Eps > 0);
        Assert.True(latest.Equity > 0);

        Console.WriteLine(
            $"ASII latest: {latest.PeriodEnd} " +
            $"Revenue={latest.Revenue} " +
            $"NetIncome={latest.NetIncome} " +
            $"EPS={latest.Eps} " +
            $"FCF={latest.FreeCashFlow} " +
            $"Equity={latest.Equity} " +
            $"Debt={latest.Debt} " +
            $"Cash={latest.Cash} " +
            $"Shares={latest.SharesOutstanding}");
    }
}
