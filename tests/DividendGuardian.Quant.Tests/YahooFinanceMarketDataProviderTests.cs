using DividendGuardian.Infrastructure;

namespace DividendGuardian.Quant.Tests;

public sealed class YahooFinanceMarketDataProviderTests
{
    [Fact]
    public async Task GetEodPricesAsync_Asii_ReturnsRecentPrices()
    {
        using var httpClient = new HttpClient();

        var provider = new YahooFinanceMarketDataProvider(httpClient);

        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-14);

        var prices = await provider.GetEodPricesAsync(
            "ASII.JK",
            from,
            to);

        Assert.NotEmpty(prices);

        var latest = prices[^1];

        Assert.Equal("ASII.JK", latest.Ticker);
        Assert.True(latest.Close > 0);
        Assert.True(latest.Volume >= 0);

        Console.WriteLine(
            $"ASII.JK latest: {latest.TradeDate} " +
            $"O={latest.Open} H={latest.High} " +
            $"L={latest.Low} C={latest.Close} " +
            $"V={latest.Volume}");
    }
}
