using DividendGuardian.Domain;

namespace DividendGuardian.Quant;

public sealed class QuantScoringEngine
{
    public QuantScore Score(
        string ticker,
        decimal currentYield,
        decimal? median5YYield,
        decimal? payoutRatio,
        decimal? fcfPayoutRatio,
        int dividendHistoryScore,
        decimal? epsCagr5Y,
        decimal? epsCagr3Y,
        decimal earningsConsistencyScore,
        decimal? peValuationScore,
        decimal? yieldValuationScore,
        decimal? fcfValuationScore,
        decimal riskScore)
    {
        var yieldScore = ScoreYieldAvailable(currentYield, median5YYield);
        var sustainability = ScoreSustainabilityAvailable(payoutRatio, fcfPayoutRatio, dividendHistoryScore);
        var growth = ScoreGrowth(epsCagr5Y, epsCagr3Y, earningsConsistencyScore);
        var valuation = ScoreValuationAvailable(peValuationScore, yieldValuationScore, fcfValuationScore);
        var risk = Clamp(riskScore, 0, 15);

        var total = Clamp(yieldScore + sustainability + growth + valuation + risk, 0, 100);
        var hasCompleteGrowthHistory = epsCagr5Y is not null && epsCagr3Y is not null;
        var status = total >= 75 && valuation >= 17 && sustainability >= 15 && hasCompleteGrowthHistory
            ? AnalysisStatus.Accumulate
            : total >= 65
                ? AnalysisStatus.Watch
                : total >= 50
                    ? AnalysisStatus.Review
                    : AnalysisStatus.Avoid;

        return new QuantScore(ticker, yieldScore, sustainability, growth, valuation, risk, total, status);
    }

    public static decimal ScoreYield(decimal currentYield, decimal median5YYield)
    {
        if (currentYield <= 0 || median5YYield <= 0) return 0;
        var ratio = currentYield / median5YYield;
        return ratio switch
        {
            < 0.75m => 3,
            < 0.90m => 6,
            < 1.00m => 9,
            < 1.10m => 12,
            _ => 15
        };
    }

    public static decimal ScoreSustainability(decimal payout, decimal fcfPayout, int historyScore)
    {
        var payoutScore = payout <= 50 ? 10 : payout <= 65 ? 8 : payout <= 75 ? 6 : payout <= 90 ? 3 : 0;
        var fcfScore = fcfPayout <= 60 ? 8 : fcfPayout <= 80 ? 6 : fcfPayout <= 100 ? 3 : 0;
        return Clamp(payoutScore + fcfScore + historyScore, 0, 25);
    }

    public static decimal ScoreGrowth(decimal? eps5Y, decimal? eps3Y, decimal consistencyScore)
    {
        var consistency = Clamp(consistencyScore, 0, 10);
        var five = eps5Y is null
            ? (decimal?)null
            : eps5Y >= 12 ? 10 : eps5Y >= 8 ? 8 : eps5Y >= 5 ? 6 : eps5Y >= 0 ? 3 : 0;
        var three = eps3Y is null
            ? (decimal?)null
            : eps3Y >= 12 ? 6 : eps3Y >= 8 ? 5 : eps3Y >= 5 ? 4 : eps3Y >= 0 ? 2 : 0;

        // Preserve the original scoring model when all growth inputs exist.
        if (five is not null && three is not null)
            return Clamp(five.Value + three.Value + consistency, 0, 20);

        // Missing history is unknown, not bad. Normalize only the available
        // growth evidence to the section's 20-point weight.
        var earned = consistency + (five ?? 0m) + (three ?? 0m);
        var available = 10m + (five is null ? 0m : 10m) + (three is null ? 0m : 6m);
        return Normalize(earned, available, 20m);
    }

    private static decimal ScoreYieldAvailable(decimal currentYield, decimal? historicalYield)
    {
        if (currentYield <= 0) return 0;
        if (historicalYield is null or <= 0) return 7.5m;
        return ScoreYield(currentYield, historicalYield.Value);
    }

    private static decimal ScoreSustainabilityAvailable(decimal? payout, decimal? fcfPayout, int historyScore)
    {
        var earned = Clamp(historyScore, 0, 7);
        var available = 7m;

        if (payout is not null)
        {
            earned += payout <= 50 ? 10 : payout <= 65 ? 8 : payout <= 75 ? 6 : payout <= 90 ? 3 : 0;
            available += 10m;
        }

        if (fcfPayout is not null)
        {
            earned += fcfPayout <= 60 ? 8 : fcfPayout <= 80 ? 6 : fcfPayout <= 100 ? 3 : 0;
            available += 8m;
        }

        return Normalize(earned, available, 25m);
    }

    private static decimal ScoreValuationAvailable(decimal? pe, decimal? yield, decimal? fcf)
    {
        var earned = 0m;
        var available = 0m;

        if (pe is not null) { earned += Clamp(pe.Value, 0, 10); available += 10m; }
        if (yield is not null) { earned += Clamp(yield.Value, 0, 8); available += 8m; }
        if (fcf is not null) { earned += Clamp(fcf.Value, 0, 7); available += 7m; }

        return available == 0 ? 0 : Normalize(earned, available, 25m);
    }

    private static decimal Normalize(decimal earned, decimal available, decimal targetWeight) =>
        available <= 0 ? 0 : Clamp(earned / available * targetWeight, 0, targetWeight);

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(max, Math.Max(min, value));
}
