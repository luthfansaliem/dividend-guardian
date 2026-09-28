using DividendGuardian.Domain;
using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class AccountingConsistencyTests
{
    [Fact]
    public void Analyze_SuppressesPerShareValuation_WhenEpsSharesAndNetIncomeUnitsAreInconsistent()
    {
        var engine = new QuantAnalysisEngine();
        var input = new QuantAnalysisInput(
            "TEST",
            25_000m,
            new[]
            {
                new AnnualFundamentalPoint(2023, 1m, 1_000_000m, 1_000_000m, 1_000_000),
                new AnnualFundamentalPoint(2024, 1m, 1_000_000m, 1_000_000m, 1_000_000),
                new AnnualFundamentalPoint(2025, 0.17m, 1_000_000m, 10_000_000_000m, 1_000_000)
            },
            new[]
            {
                new AnnualDividendPoint(2023, 100m),
                new AnnualDividendPoint(2024, 100m),
                new AnnualDividendPoint(2025, 100m)
            },
            new[]
            {
                new AnnualPricePoint(2023, 20_000m),
                new AnnualPricePoint(2024, 22_000m),
                new AnnualPricePoint(2025, 25_000m)
            },
            IsCyclical: true);

        var result = engine.Analyze(input);

        Assert.Equal("INCONSISTENT", result.DataQuality);
        Assert.Null(result.CurrentPe);
        Assert.Null(result.Metrics.PayoutRatio);
        Assert.Null(result.Metrics.FcfPayoutRatio);
        Assert.Contains(result.Reasons, x => x.Contains("Accounting consistency check failed"));
        Assert.Equal(AnalysisStatus.Review, result.Score.Status);
    }

    [Fact]
    public void Analyze_UsesReviewStatus_WhenFundamentalAndMarketCurrenciesDiffer()
    {
        var engine = new QuantAnalysisEngine();
        var input = new QuantAnalysisInput(
            "ITMG",
            25_550m,
            new[]
            {
                new AnnualFundamentalPoint(2023, 2m, 2_000_000m, 2_000_000m, 1_000_000),
                new AnnualFundamentalPoint(2024, 2m, 2_000_000m, 2_000_000m, 1_000_000),
                new AnnualFundamentalPoint(2025, 2m, 2_000_000m, 2_000_000m, 1_000_000)
            },
            new[]
            {
                new AnnualDividendPoint(2023, 1_000m),
                new AnnualDividendPoint(2024, 1_000m),
                new AnnualDividendPoint(2025, 1_000m)
            },
            new[]
            {
                new AnnualPricePoint(2023, 20_000m),
                new AnnualPricePoint(2024, 22_000m),
                new AnnualPricePoint(2025, 25_000m)
            },
            IsCyclical: true,
            FundamentalCurrency: "USD",
            MarketCurrency: "IDR");

        var result = engine.Analyze(input);

        Assert.Equal("CURRENCY_MISMATCH", result.DataQuality);
        Assert.Equal(AnalysisStatus.Review, result.Score.Status);
        Assert.Null(result.CurrentPe);
        Assert.Null(result.Metrics.PayoutRatio);
        Assert.Null(result.Metrics.FcfPayoutRatio);
        Assert.Null(result.FairValue.Base);
    }
}
