using DividendGuardian.Domain;

namespace DividendGuardian.Quant;

public sealed class QuantScoringEngine
{
    public QuantScore Score(
        string ticker,
        decimal currentYield,
        decimal median5YYield,
        decimal payoutRatio,
        decimal fcfPayoutRatio,
        int dividendHistoryScore,
        decimal epsCagr5Y,
        decimal epsCagr3Y,
        decimal earningsConsistencyScore,
        decimal peValuationScore,
        decimal yieldValuationScore,
        decimal fcfValuationScore,
        decimal riskScore)
    {
        var yieldScore = ScoreYield(currentYield, median5YYield);
        var sustainability = ScoreSustainability(payoutRatio, fcfPayoutRatio, dividendHistoryScore);
        var growth = ScoreGrowth(epsCagr5Y, epsCagr3Y, earningsConsistencyScore);
        var valuation = Clamp(peValuationScore + yieldValuationScore + fcfValuationScore, 0, 25);
        var risk = Clamp(riskScore, 0, 15);

        var total = Clamp(yieldScore + sustainability + growth + valuation + risk, 0, 100);
        var status = total >= 75 && valuation >= 17 && sustainability >= 15
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

    public static decimal ScoreGrowth(decimal eps5Y, decimal eps3Y, decimal consistencyScore)
    {
        var five = eps5Y >= 12 ? 10 : eps5Y >= 8 ? 8 : eps5Y >= 5 ? 6 : eps5Y >= 0 ? 3 : 0;
        var three = eps3Y >= 12 ? 6 : eps3Y >= 8 ? 5 : eps3Y >= 5 ? 4 : eps3Y >= 0 ? 2 : 0;
        return Clamp(five + three + consistencyScore, 0, 20);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(max, Math.Max(min, value));
}
