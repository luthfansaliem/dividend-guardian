namespace DividendGuardian.Quant;

public sealed record AnnualFundamentalPoint(
    int Year,
    decimal Eps,
    decimal FreeCashFlow,
    decimal NetIncome,
    long SharesOutstanding,
    decimal Debt = 0m);

public sealed record AnnualDividendPoint(
    int Year,
    decimal Dps);

public sealed record DividendQualityMetrics(
    decimal? PayoutRatio,
    decimal? FcfPayoutRatio,
    decimal? EpsCagr3Y,
    decimal? EpsCagr5Y,
    decimal? DividendCagr5Y,
    int StableOrGrowingYears,
    decimal EarningsConsistencyScore);

public sealed class FundamentalMetricsEngine
{
    public DividendQualityMetrics Calculate(
        IReadOnlyList<AnnualFundamentalPoint> fundamentals,
        IReadOnlyList<AnnualDividendPoint> dividends)
    {
        var orderedFundamentals = fundamentals.OrderBy(x => x.Year).ToArray();
        var orderedDividends = dividends.OrderBy(x => x.Year).ToArray();

        var latest = orderedFundamentals.LastOrDefault();
        var latestDividend = orderedDividends.LastOrDefault();

        decimal? payout = latest is not null && latest.NetIncome > 0 && latest.SharesOutstanding > 0 && latestDividend is not null
            ? latestDividend.Dps * latest.SharesOutstanding / latest.NetIncome
            : null;

        decimal? fcfPayout = latest is not null && latest.FreeCashFlow > 0 && latest.SharesOutstanding > 0 && latestDividend is not null
            ? latestDividend.Dps * latest.SharesOutstanding / latest.FreeCashFlow
            : null;

        var eps3 = CagrFromYears(orderedFundamentals.Select(x => (x.Year, x.Eps)), 3);
        var eps5 = CagrFromYears(orderedFundamentals.Select(x => (x.Year, x.Eps)), 5);
        var div5 = CagrFromYears(orderedDividends.Select(x => (x.Year, x.Dps)), 5);

        var stability = CalculateStableOrGrowingYears(orderedDividends);
        var consistency = CalculateEarningsConsistency(orderedFundamentals);

        return new DividendQualityMetrics(
            payout is null ? null : payout.Value * 100m,
            fcfPayout is null ? null : fcfPayout.Value * 100m,
            eps3,
            eps5,
            div5,
            stability,
            consistency);
    }

    public static decimal? Cagr(decimal start, decimal end, int years)
    {
        if (years <= 0 || start <= 0 || end <= 0) return null;
        return (decimal)(Math.Pow((double)(end / start), 1d / years) - 1d) * 100m;
    }

    public static decimal? CagrFromYears(IEnumerable<(int Year, decimal Value)> values, int years)
    {
        var points = values.OrderBy(x => x.Year).ToArray();
        if (points.Length == 0) return null;

        var end = points[^1];
        var targetYear = end.Year - years;
        var start = points.FirstOrDefault(x => x.Year == targetYear);

        return start == default ? null : Cagr(start.Value, end.Value, years);
    }

    public static int CalculateStableOrGrowingYears(IReadOnlyList<AnnualDividendPoint> dividends)
    {
        if (dividends.Count < 2) return dividends.Count;
        var ordered = dividends.OrderBy(x => x.Year).ToArray();
        var count = 1;
        for (var i = ordered.Length - 1; i > 0; i--)
        {
            if (ordered[i].Dps >= ordered[i - 1].Dps) count++;
            else break;
        }
        return count;
    }

    public static decimal CalculateEarningsConsistency(IReadOnlyList<AnnualFundamentalPoint> fundamentals)
    {
        var ordered = fundamentals.OrderBy(x => x.Year).ToArray();
        if (ordered.Length < 2) return 0;

        var positiveYears = ordered.Count(x => x.Eps > 0);
        var nonDecliningTransitions = 0;
        for (var i = 1; i < ordered.Length; i++)
            if (ordered[i].Eps >= ordered[i - 1].Eps) nonDecliningTransitions++;

        var positiveScore = 5m * positiveYears / ordered.Length;
        var trendScore = 5m * nonDecliningTransitions / (ordered.Length - 1);
        return Math.Min(10m, positiveScore + trendScore);
    }
}