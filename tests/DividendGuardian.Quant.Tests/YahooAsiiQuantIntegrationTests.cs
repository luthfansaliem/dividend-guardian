using DividendGuardian.Infrastructure;
using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class YahooAsiiQuantIntegrationTests
{
    [Fact]
    public async Task Analyze_Asii_UsingYahooData_ProducesRealValuation()
    {
        using var httpClient = new HttpClient();

        var market = new YahooFinanceMarketDataProvider(httpClient);
        var fundamentalsProvider = new YahooFinanceFundamentalDataProvider(httpClient);

        var from = new DateOnly(2021, 1, 1);
        var to = DateOnly.FromDateTime(DateTime.UtcNow);

        var prices = await market.GetEodPricesAsync("ASII.JK", from, to);
        var fundamentals = await fundamentalsProvider.GetAnnualFundamentalsAsync("ASII.JK", from, to);
        var dividends = await fundamentalsProvider.GetDividendsAsync("ASII.JK", from, to);

        Assert.NotEmpty(prices);
        Assert.NotEmpty(fundamentals);
        Assert.NotEmpty(dividends);

        var latestPrice = prices.OrderBy(x => x.TradeDate).Last();
        var annualPrices = prices
            .GroupBy(x => x.TradeDate.Year)
            .Select(g => new AnnualPricePoint(g.Key, g.OrderBy(x => x.TradeDate).Last().Close))
            .OrderBy(x => x.Year)
            .ToArray();

        var annualFundamentals = fundamentals
            .Where(x => x.PeriodEnd.Year >= 2021)
            .Where(x => x.Eps > 0 && x.SharesOutstanding > 0)
            .Select(x => new AnnualFundamentalPoint(
                x.PeriodEnd.Year,
                x.Eps,
                x.FreeCashFlow,
                x.NetIncome,
                x.SharesOutstanding,
                x.Debt))
            .GroupBy(x => x.Year)
            .Select(g => g.OrderBy(x => x.Year).Last())
            .OrderBy(x => x.Year)
            .ToArray();

        var annualDividends = dividends
            .Select(x => new AnnualDividendPoint(x.FiscalYear, x.Dps))
            .Where(x => x.Dps > 0)
            .OrderBy(x => x.Year)
            .ToArray();

        Assert.True(annualFundamentals.Length >= 3);
        Assert.True(annualDividends.Length >= 3);
        Assert.True(annualPrices.Length >= 3);

        var result = new QuantAnalysisEngine().Analyze(new QuantAnalysisInput(
            "ASII.JK",
            latestPrice.Close,
            annualFundamentals,
            annualDividends,
            annualPrices,
            false));

        Assert.Equal("ASII.JK", result.Score.Ticker);
        Assert.True(result.CurrentPrice > 0);
        Assert.True(result.CurrentDividendYieldPercent > 0);
        Assert.True(result.CurrentPe > 0);
        Assert.True(result.CurrentFcfYieldPercent > 0);
        Assert.NotNull(result.FairValue.Base);
        Assert.NotNull(result.FairValue.Conservative);
        Assert.NotNull(result.FairValue.Optimistic);
        Assert.NotNull(result.MarginOfSafety);
        Assert.NotEqual("LIMITED", result.DataQuality);

        Console.WriteLine($"ASII.JK current price = {result.CurrentPrice:F2}");
        Console.WriteLine($"ASII.JK dividend yield = {result.CurrentDividendYieldPercent:F2}%");
        Console.WriteLine($"ASII.JK PE = {result.CurrentPe:F2}x");
        Console.WriteLine($"ASII.JK FCF yield = {result.CurrentFcfYieldPercent:F2}%");
        var fairValueAudit = new FairValueEngine().Calculate(new FairValueInput(
            annualDividends.Last().Dps,
            annualFundamentals.Last().Eps,
            annualFundamentals.Last().FreeCashFlow / annualFundamentals.Last().SharesOutstanding,
            result.HistoricalMedianDividendYieldPercent,
            result.HistoricalMedianPe,
            result.HistoricalMedianFcfYieldPercent));

        Console.WriteLine($"ASII.JK historical median dividend yield = {result.HistoricalMedianDividendYieldPercent:F2}%");
        Console.WriteLine($"ASII.JK historical median PE = {result.HistoricalMedianPe:F2}x");
        Console.WriteLine($"ASII.JK historical median FCF yield = {result.HistoricalMedianFcfYieldPercent:F2}%");
        Console.WriteLine($"ASII.JK dividend fair value = {fairValueAudit.DividendYieldFairValue:F2}");
        Console.WriteLine($"ASII.JK PE fair value = {fairValueAudit.PeFairValue:F2}");
        Console.WriteLine($"ASII.JK FCF fair value = {fairValueAudit.FcfFairValue:F2}");
        Console.WriteLine($"ASII.JK fair value conservative = {result.FairValue.Conservative:F2}");
        Console.WriteLine($"ASII.JK fair value base = {result.FairValue.Base:F2}");
        Console.WriteLine($"ASII.JK fair value optimistic = {result.FairValue.Optimistic:F2}");
        Console.WriteLine($"ASII.JK margin of safety = {result.MarginOfSafety:P1}");
        Console.WriteLine($"ASII.JK buy zone = {result.BuyZone.Status}");
        Console.WriteLine($"ASII.JK score = {result.Score.TotalScore:F1}");
        Console.WriteLine($"ASII.JK data quality = {result.DataQuality}");
    }
}