using DividendGuardian.AI;
using DividendGuardian.Infrastructure;
using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class RealAiAsiiIntegrationTests
{
    [Fact]
    public async Task Analyze_Asii_WithRealOpenAi()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_REAL_AI_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        Assert.False(
            string.IsNullOrWhiteSpace(apiKey),
            "OPENAI_API_KEY must be configured when RUN_REAL_AI_TESTS=true.");

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        var market = new YahooFinanceMarketDataProvider(httpClient);
        var fundamentalsProvider = new YahooFinanceFundamentalDataProvider(httpClient);

        var from = new DateOnly(2020, 1, 1);
        var to = DateOnly.FromDateTime(DateTime.UtcNow);

        var prices = await market.GetEodPricesAsync("ASII.JK", from, to);
        var fundamentals = await fundamentalsProvider.GetAnnualFundamentalsAsync("ASII.JK", from, to);
        var dividends = await fundamentalsProvider.GetDividendsAsync("ASII.JK", from, to);

        var latestPrice = prices.OrderBy(x => x.TradeDate).Last();

        var annualPrices = prices
            .GroupBy(x => x.TradeDate.Year)
            .Select(g => new AnnualPricePoint(
                g.Key,
                g.OrderBy(x => x.TradeDate).Last().Close))
            .OrderBy(x => x.Year)
            .ToArray();

        var annualFundamentals = fundamentals
            .Where(x => x.PeriodEnd.Year >= 2020)
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

        var quant = new QuantAnalysisEngine().Analyze(
            new QuantAnalysisInput(
                "ASII.JK",
                latestPrice.Close,
                annualFundamentals,
                annualDividends,
                annualPrices,
                false));

        var aiOptions = new AiOptions
        {
            ApiKey = apiKey!,
            Model = Environment.GetEnvironmentVariable("OPENAI_MODEL")
                     ?? "gpt-5.6-luna",
            MaxRetries = 1,
            RetryDelayMs = 500
        };

        var analyst = new AiAnalyst(httpClient, aiOptions);

        var request = new AiAnalysisRequest(
            "ASII.JK",
            DateOnly.FromDateTime(DateTime.UtcNow),
            quant,
            new[]
            {
                new AiTriggerEvent(
                    AiAnalysisTrigger.ManualReview,
                    DateTimeOffset.UtcNow,
                    "Manual real-AI integration test.")
            });

        var result = await analyst.AnalyzeAsync(request);

        Assert.Equal("ASII.JK", result.Ticker);
        Assert.False(string.IsNullOrWhiteSpace(result.Verdict));
        Assert.False(string.IsNullOrWhiteSpace(result.WhyAccumulate));
        Assert.False(string.IsNullOrWhiteSpace(result.WhyNotAccumulate));
        Assert.False(string.IsNullOrWhiteSpace(result.DataQuality));

        Console.WriteLine();
        Console.WriteLine("========== REAL AI RESULT ==========");
        Console.WriteLine($"Ticker       : {result.Ticker}");
        Console.WriteLine($"Verdict      : {result.Verdict}");
        Console.WriteLine($"Data Quality : {result.DataQuality}");
        Console.WriteLine($"Model        : {result.Model}");
        Console.WriteLine($"Prompt       : {result.PromptVersion}");
        Console.WriteLine();
        Console.WriteLine("WHY ACCUMULATE:");
        Console.WriteLine(result.WhyAccumulate);
        Console.WriteLine();
        Console.WriteLine("WHY NOT ACCUMULATE:");
        Console.WriteLine(result.WhyNotAccumulate);
        Console.WriteLine();
        Console.WriteLine("RISKS:");
        foreach (var risk in result.KeyRisks)
            Console.WriteLine($"- {risk}");
        Console.WriteLine();
        Console.WriteLine("DATA GAPS:");
        foreach (var gap in result.DataGaps)
            Console.WriteLine($"- {gap}");
        Console.WriteLine();
        Console.WriteLine("INVALIDATION:");
        foreach (var trigger in result.InvalidationTriggers)
            Console.WriteLine($"- {trigger}");
        Console.WriteLine("====================================");
    }
}
