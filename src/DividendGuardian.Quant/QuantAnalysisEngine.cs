using System.Globalization;
using DividendGuardian.Domain;

namespace DividendGuardian.Quant;

public sealed record AnnualPricePoint(int Year, decimal Close);

public sealed record QuantAnalysisInput(
    string Ticker,
    decimal CurrentPrice,
    IReadOnlyList<AnnualFundamentalPoint> Fundamentals,
    IReadOnlyList<AnnualDividendPoint> Dividends,
    IReadOnlyList<AnnualPricePoint> HistoricalYearEndPrices,
    bool IsCyclical,
    string? FundamentalCurrency = null,
    string? MarketCurrency = "IDR");

public sealed record QuantAnalysisResult(
    QuantScore Score,
    DividendQualityMetrics Metrics,
    decimal CurrentPrice,
    decimal CurrentDividendYieldPercent,
    decimal? HistoricalMedianDividendYieldPercent,
    decimal? CurrentPe,
    decimal? HistoricalMedianPe,
    decimal? CurrentFcfYieldPercent,
    decimal? HistoricalMedianFcfYieldPercent,
    decimal RiskScore,
    FairValueRange FairValue,
    ValuationConfidenceMetrics ValuationConfidence,
    decimal? MarginOfSafety,
    BuyZone BuyZone,
    IReadOnlyList<string> Reasons,
    string DataQuality)
{
    public QuantAnalysisResult(
        QuantScore score,
        DividendQualityMetrics metrics,
        decimal currentPrice,
        decimal currentDividendYieldPercent,
        decimal? historicalMedianDividendYieldPercent,
        decimal? currentPe,
        decimal? historicalMedianPe,
        decimal? currentFcfYieldPercent,
        decimal? historicalMedianFcfYieldPercent,
        decimal riskScore,
        FairValueRange fairValue,
        decimal? marginOfSafety,
        BuyZone buyZone,
        IReadOnlyList<string> reasons,
        string dataQuality)
        : this(
            score,
            metrics,
            currentPrice,
            currentDividendYieldPercent,
            historicalMedianDividendYieldPercent,
            currentPe,
            historicalMedianPe,
            currentFcfYieldPercent,
            historicalMedianFcfYieldPercent,
            riskScore,
            fairValue,
            new ValuationConfidenceMetrics(0, null, null, null, "UNKNOWN"),
            marginOfSafety,
            buyZone,
            reasons,
            dataQuality)
    {
    }
};

public sealed class QuantAnalysisEngine
{
    private readonly FundamentalMetricsEngine _metrics = new();
    private readonly QuantScoringEngine _scoring = new();
    private readonly FairValueEngine _fairValue = new();
    private readonly BuyZoneEngine _buyZone = new();

    public QuantAnalysisResult Analyze(QuantAnalysisInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Ticker))
            throw new ArgumentException("Ticker is required.", nameof(input));
        if (input.CurrentPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(input.CurrentPrice));
        if (input.Fundamentals.Count == 0)
            throw new ArgumentException("At least one annual fundamental point is required.", nameof(input.Fundamentals));

        var fundamentals = input.Fundamentals.OrderBy(x => x.Year).ToArray();
        var dividends = input.Dividends.OrderBy(x => x.Year).ToArray();
        var prices = input.HistoricalYearEndPrices
            .Where(x => x.Close > 0)
            .GroupBy(x => x.Year)
            .Select(g => g.Last())
            .OrderBy(x => x.Year)
            .ToArray();

        var metrics = _metrics.Calculate(fundamentals, dividends);
        var latest = fundamentals[^1];
        var currencyConsistent = string.IsNullOrWhiteSpace(input.FundamentalCurrency) ||
                                 string.IsNullOrWhiteSpace(input.MarketCurrency) ||
                                 input.FundamentalCurrency.Equals(input.MarketCurrency, StringComparison.OrdinalIgnoreCase);
        var accountingConsistent = currencyConsistent && IsEpsSharesConsistent(latest);
        if (!accountingConsistent)
        {
            metrics = metrics with { PayoutRatio = null, FcfPayoutRatio = null };
        }

        var latestDps = dividends.LastOrDefault()?.Dps;
        var currentYield = latestDps is > 0
            ? latestDps.Value / input.CurrentPrice * 100m
            : 0m;

        var historicalYield = Median(
            dividends.Join(prices, d => d.Year, p => p.Year,
                (d, p) => new { d.Year, Yield = d.Dps / p.Close * 100m })
            .Where(x => x.Yield > 0)
            .OrderBy(x => x.Year)
            .TakeLast(5)
            .Select(x => x.Yield));

        decimal? currentPe = accountingConsistent && latest.Eps > 0 ? (decimal?)(input.CurrentPrice / latest.Eps) : null;

        var historicalPe = Median(
            fundamentals.Where(IsEpsSharesConsistent).Join(prices, f => f.Year, p => p.Year,
                (f, p) => new { f.Year, Value = f.Eps > 0 ? p.Close / f.Eps : 0m })
            .Where(x => x.Value > 0)
            .OrderBy(x => x.Year)
            .TakeLast(5)
            .Select(x => x.Value));

        decimal? currentFcfYield = latest.FreeCashFlow > 0 && latest.SharesOutstanding > 0
            ? (decimal?)(latest.FreeCashFlow / (input.CurrentPrice * latest.SharesOutstanding) * 100m)
            : null;

        var historicalFcfYield = Median(
            fundamentals.Join(prices, f => f.Year, p => p.Year,
                (f, p) => new
                {
                    f.Year,
                    Value = f.FreeCashFlow > 0 && f.SharesOutstanding > 0
                        ? f.FreeCashFlow / (p.Close * f.SharesOutstanding) * 100m
                        : 0m
                })
            .Where(x => x.Value > 0)
            .OrderBy(x => x.Year)
            .TakeLast(5)
            .Select(x => x.Value));

        var peScore = ScorePe(currentPe, historicalPe);
        var yieldScore = ScoreRelative(currentYield, historicalYield, 8m);
        var fcfScore = ScoreRelative(currentFcfYield, historicalFcfYield, 7m);
        var riskScore = ScoreRisk(latest, input.IsCyclical);

        var score = _scoring.Score(
            input.Ticker,
            currentYield / 100m,
            (historicalYield ?? currentYield) / 100m,
            metrics.PayoutRatio ?? 100m,
            metrics.FcfPayoutRatio ?? 100m,
            Math.Min(7, metrics.StableOrGrowingYears),
            metrics.EpsCagr5Y,
            metrics.EpsCagr3Y,
            metrics.EarningsConsistencyScore,
            peScore, yieldScore, fcfScore, riskScore);

        var fairValue = _fairValue.Calculate(new FairValueInput(
            LatestFiscalYearDps: latestDps > 0 ? latestDps : null,
            LatestFiscalYearEps: accountingConsistent && latest.Eps > 0 ? latest.Eps : null,
            LatestFiscalYearFcfPerShare: latest.FreeCashFlow > 0 && latest.SharesOutstanding > 0
                ? latest.FreeCashFlow / latest.SharesOutstanding
                : null,
            HistoricalMedianDividendYieldPercent: historicalYield,
            HistoricalMedianPe: historicalPe,
            HistoricalMedianFcfYieldPercent: historicalFcfYield));

        var marginOfSafety = FairValueEngine.MarginOfSafety(
            input.CurrentPrice, fairValue.Range.Conservative);

        var dataQuality = !currencyConsistent
            ? "CURRENCY_MISMATCH"
            : accountingConsistent
                ? DetermineDataQuality(fundamentals, dividends, prices, metrics, historicalYield, historicalPe, historicalFcfYield, fairValue)
                : "INCONSISTENT";

        if (dataQuality is "CURRENCY_MISMATCH" or "INCONSISTENT")
            score = score with { Status = AnalysisStatus.Review };

        var buyZone = _buyZone.Evaluate(
            input.CurrentPrice,
            fairValue.Range.Conservative,
            fairValue.Range.Base,
            fairValue.Range.Optimistic,
            score.TotalScore,
            fairValue.Confidence.Level,
            dataQuality);
        var reasons = BuildReasons(currentYield, historicalYield, currentPe, historicalPe,
            currentFcfYield, metrics, riskScore, fairValue, marginOfSafety);
        if (!currencyConsistent)
            reasons = reasons.Append($"Currency mismatch: fundamentals are {input.FundamentalCurrency} while market/dividend values are {input.MarketCurrency}; cross-currency PE, payout, and fair-value metrics were suppressed.").ToArray();
        else if (!accountingConsistent)
            reasons = reasons.Append("Accounting consistency check failed: EPS × shares is materially inconsistent with net income; PE and payout metrics that depend on these units were suppressed.").ToArray();
        reasons = reasons.Append($"Data quality {dataQuality}.").ToArray();

        return new QuantAnalysisResult(
            score, metrics, input.CurrentPrice, currentYield, historicalYield,
            currentPe, historicalPe, currentFcfYield, historicalFcfYield, riskScore,
            fairValue.Range, fairValue.Confidence, marginOfSafety, buyZone, reasons, dataQuality);
    }

    private static bool IsEpsSharesConsistent(AnnualFundamentalPoint point)
    {
        if (point.Eps <= 0 || point.SharesOutstanding <= 0 || point.NetIncome <= 0)
            return false;

        var impliedNetIncome = point.Eps * point.SharesOutstanding;
        var ratio = impliedNetIncome / point.NetIncome;
        return ratio >= 0.5m && ratio <= 2m;
    }

    private static string DetermineDataQuality(
        IReadOnlyList<AnnualFundamentalPoint> fundamentals,
        IReadOnlyList<AnnualDividendPoint> dividends,
        IReadOnlyList<AnnualPricePoint> prices,
        DividendQualityMetrics metrics,
        decimal? historicalYield,
        decimal? historicalPe,
        decimal? historicalFcfYield,
        FairValueResult fairValue)
    {
        if (fundamentals.Count < 3 || dividends.Count < 3 || prices.Count < 3)
            return "LIMITED";

        if (historicalYield is null || historicalPe is null || historicalFcfYield is null ||
            fairValue.Range.Base is null || metrics.EpsCagr5Y is null)
            return "PARTIAL";

        return "READY";
    }

    private static decimal ScorePe(decimal? current, decimal? historical)
    {
        if (current is null || historical is null || current <= 0 || historical <= 0) return 0;
        var ratio = current.Value / historical.Value;
        return ratio <= .70m ? 10m : ratio <= .85m ? 8m : ratio <= 1m ? 6m : 
               ratio <= 1.15m ? 4m : ratio <= 1.30m ? 2m : 0m;
    }

    private static decimal ScoreRelative(decimal? current, decimal? historical, decimal max)
    {
        if (current is null || current <= 0) return 0;
        if (historical is null || historical <= 0) return max * .5m;
        var ratio = current.Value / historical.Value;
        return ratio >= 1.30m ? max : ratio >= 1.15m ? max * .85m :
               ratio >= 1.00m ? max * .70m : ratio >= .85m ? max * .45m : max * .20m;
    }

    private static decimal ScoreRisk(AnnualFundamentalPoint latest, bool cyclical)
    {
        var score = 15m;
        if (latest.FreeCashFlow <= 0) score -= 5m;
        else if (latest.Debt > latest.FreeCashFlow * 4m) score -= 4m;
        else if (latest.Debt > latest.FreeCashFlow * 2m) score -= 2m;
        if (cyclical) score -= 2m;
        if (latest.NetIncome <= 0) score -= 4m;
        return Math.Max(0, Math.Min(15, score));
    }

    private static IReadOnlyList<string> BuildReasons(
        decimal currentYield,
        decimal? historicalYield,
        decimal? currentPe,
        decimal? historicalPe,
        decimal? currentFcfYield,
        DividendQualityMetrics metrics,
        decimal riskScore,
        FairValueResult fairValue,
        decimal? marginOfSafety)
    {
        var reasons = new List<string>();
        reasons.Add(historicalYield is not null
            ? $"Dividend yield {currentYield.ToString("F2", CultureInfo.InvariantCulture)}% vs historical median {historicalYield.Value.ToString("F2", CultureInfo.InvariantCulture)}%."
            : $"Dividend yield {currentYield.ToString("F2", CultureInfo.InvariantCulture)}%; historical yield baseline unavailable.");
        if (currentPe is not null && historicalPe is not null)
            reasons.Add($"PE {currentPe.Value.ToString("F2", CultureInfo.InvariantCulture)}x vs historical median {historicalPe.Value.ToString("F2", CultureInfo.InvariantCulture)}x.");
        if (currentFcfYield is not null) reasons.Add($"FCF yield {currentFcfYield.Value.ToString("F2", CultureInfo.InvariantCulture)}%.");
        if (metrics.PayoutRatio is not null) reasons.Add($"Payout ratio {metrics.PayoutRatio.Value.ToString("F1", CultureInfo.InvariantCulture)}%.");
        if (metrics.FcfPayoutRatio is not null) reasons.Add($"FCF payout {metrics.FcfPayoutRatio.Value.ToString("F1", CultureInfo.InvariantCulture)}%.");
        if (metrics.EpsCagr5Y is not null) reasons.Add($"EPS CAGR 5Y {metrics.EpsCagr5Y.Value.ToString("F1", CultureInfo.InvariantCulture)}%.");
        if (fairValue.Range.Base is not null)
            reasons.Add($"Fair value base {fairValue.Range.Base.Value.ToString("F2", CultureInfo.InvariantCulture)}; range {fairValue.Range.Conservative!.Value.ToString("F2", CultureInfo.InvariantCulture)}-{fairValue.Range.Optimistic!.Value.ToString("F2", CultureInfo.InvariantCulture)}.");
        else
            reasons.Add("Fair value unavailable: insufficient valuation baseline data.");
        if (marginOfSafety is not null)
            reasons.Add($"Margin of safety vs conservative fair value {marginOfSafety.Value.ToString("P1", CultureInfo.InvariantCulture)}.");
        reasons.Add($"Valuation confidence {fairValue.Confidence.Level}; method spread {fairValue.Confidence.SpreadPercent?.ToString("F1", CultureInfo.InvariantCulture) ?? "N/A"}%.");
        reasons.Add($"Risk score {riskScore.ToString("F1", CultureInfo.InvariantCulture)}/15.");
        return reasons;
    }

    private static decimal? Median(IEnumerable<decimal> values)
    {
        var ordered = values.Where(x => x > 0).OrderBy(x => x).ToArray();
        if (ordered.Length == 0) return null;
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2m : ordered[middle];
    }
}