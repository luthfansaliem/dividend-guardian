using DividendGuardian.Domain;

namespace DividendGuardian.Quant;

public sealed record AnnualPricePoint(int Year, decimal Close);

public sealed record QuantAnalysisInput(
    string Ticker,
    decimal CurrentPrice,
    IReadOnlyList<AnnualFundamentalPoint> Fundamentals,
    IReadOnlyList<AnnualDividendPoint> Dividends,
    IReadOnlyList<AnnualPricePoint> HistoricalYearEndPrices,
    bool IsCyclical);

public sealed record QuantAnalysisResult(
    QuantScore Score,
    DividendQualityMetrics Metrics,
    decimal CurrentDividendYieldPercent,
    decimal? HistoricalMedianDividendYieldPercent,
    decimal? CurrentPe,
    decimal? HistoricalMedianPe,
    decimal? CurrentFcfYieldPercent,
    decimal? HistoricalMedianFcfYieldPercent,
    decimal RiskScore,
    IReadOnlyList<string> Reasons);

public sealed class QuantAnalysisEngine
{
    private readonly FundamentalMetricsEngine _metrics = new();
    private readonly QuantScoringEngine _scoring = new();

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

        var latestDps = dividends.LastOrDefault()?.Dps;
        var currentYield = latestDps is > 0
            ? latestDps.Value / input.CurrentPrice * 100m
            : 0m;

        var historicalYield = Median(
            dividends.Join(
                prices,
                d => d.Year,
                p => p.Year,
                (d, p) => d.Dps / p.Close * 100m)
            .Where(x => x > 0));

        decimal? currentPe = latest.Eps > 0
            ? input.CurrentPrice / latest.Eps
            : null;

        var historicalPe = Median(
            fundamentals.Join(
                prices,
                f => f.Year,
                p => p.Year,
                (f, p) => f.Eps > 0 ? p.Close / f.Eps : 0m)
            .Where(x => x > 0));

        decimal? currentFcfYield = latest.FreeCashFlow > 0 && latest.SharesOutstanding > 0
            ? latest.FreeCashFlow / (input.CurrentPrice * latest.SharesOutstanding) * 100m
            : null;

        var historicalFcfYield = Median(
            fundamentals.Join(
                prices,
                f => f.Year,
                p => p.Year,
                (f, p) => f.FreeCashFlow > 0 && f.SharesOutstanding > 0
                    ? f.FreeCashFlow / (p.Close * f.SharesOutstanding) * 100m
                    : 0m)
            .Where(x => x > 0));

        var peScore = ScorePe(currentPe, historicalPe);
        var yieldScore = ScoreRelative(currentYield, historicalYield, 8m);
        var fcfScore = ScoreRelative(currentFcfYield, historicalFcfYield, 7m);
        var riskScore = ScoreRisk(latest, input.IsCyclical);

        var payout = metrics.PayoutRatio ?? 100m;
        var fcfPayout = metrics.FcfPayoutRatio ?? 100m;
        var historyScore = Math.Min(7, metrics.StableOrGrowingYears);
        var eps5 = metrics.EpsCagr5Y ?? 0m;
        var eps3 = metrics.EpsCagr3Y ?? 0m;

        var score = _scoring.Score(
            input.Ticker,
            currentYield / 100m,
            (historicalYield ?? currentYield) / 100m,
            payout,
            fcfPayout,
            historyScore,
            eps5,
            eps3,
            metrics.EarningsConsistencyScore,
            peScore,
            yieldScore,
            fcfScore,
            riskScore);

        var reasons = BuildReasons(
            currentYield, historicalYield, currentPe, historicalPe,
            currentFcfYield, metrics, riskScore);

        return new QuantAnalysisResult(
            score, metrics, currentYield, historicalYield, currentPe, historicalPe,
            currentFcfYield, historicalFcfYield, riskScore, reasons);
    }

    private static decimal ScorePe(decimal? current, decimal? historical)
    {
        if (current is null || historical is null || current <= 0 || historical <= 0) return 0;
        var ratio = current.Value / historical.Value;
        return ratio <= .70m ? 10m
            : ratio <= .85m ? 8m
            : ratio <= 1m ? 6m
            : ratio <= 1.15m ? 4m
            : ratio <= 1.30m ? 2m
            : 0m;
    }

    private static decimal ScoreRelative(decimal? current, decimal? historical, decimal max)
    {
        if (current is null || current <= 0) return 0;
        if (historical is null || historical <= 0) return max * .5m;
        var ratio = current.Value / historical.Value;
        return ratio >= 1.30m ? max
            : ratio >= 1.15m ? max * .85m
            : ratio >= 1.00m ? max * .70m
            : ratio >= .85m ? max * .45m
            : max * .20m;
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
        decimal riskScore)
    {
        var reasons = new List<string>();

        if (historicalYield is not null)
            reasons.Add($"Dividend yield {currentYield:F2}% vs historical median {historicalYield:F2}%.");
        else
            reasons.Add($"Dividend yield {currentYield:F2}%; historical yield baseline unavailable.");

        if (currentPe is not null && historicalPe is not null)
            reasons.Add($"PE {currentPe:F2}x vs historical median {historicalPe:F2}x.");

        if (currentFcfYield is not null)
            reasons.Add($"FCF yield {currentFcfYield:F2}%.");

        if (metrics.PayoutRatio is not null)
            reasons.Add($"Payout ratio {metrics.PayoutRatio:F1}%.");

        if (metrics.FcfPayoutRatio is not null)
            reasons.Add($"FCF payout {metrics.FcfPayoutRatio:F1}%.");

        if (metrics.EpsCagr5Y is not null)
            reasons.Add($"EPS CAGR 5Y {metrics.EpsCagr5Y:F1}%.");

        reasons.Add($"Risk score {riskScore:F1}/15.");
        return reasons;
    }

    private static decimal? Median(IEnumerable<decimal> values)
    {
        var ordered = values.Where(x => x > 0).OrderBy(x => x).ToArray();
        if (ordered.Length == 0) return null;
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2m
            : ordered[middle];
    }
}