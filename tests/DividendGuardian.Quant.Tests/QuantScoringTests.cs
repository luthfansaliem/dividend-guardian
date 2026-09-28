using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class QuantScoringTests
{
    [Fact] public void YieldScoreUsesHistoricalMedian()
    {
        Assert.Equal(15m,QuantScoringEngine.ScoreYield(.06m,.05m));
        Assert.Equal(12m,QuantScoringEngine.ScoreYield(.05m,.05m));
    }

    [Fact] public void SustainabilityIsCappedAt25()
        => Assert.Equal(25m,QuantScoringEngine.ScoreSustainability(40m,40m,10));

    [Fact]
    public void GrowthDoesNotAwardFiveYearGrowthPointsForMissingCagr()
        => Assert.Equal(10.3m, QuantScoringEngine.ScoreGrowth(null, 4.2m, 8.3m));

    [Fact]
    public void GrowthAwardsExpectedPointsForKnownCagr()
        => Assert.Equal(13.3m, QuantScoringEngine.ScoreGrowth(4.2m, 4.2m, 8.3m));

    [Fact]
    public void AsiiScore_IsWatchEvenWhenBuyZoneCanBeAccumulate()
    {
        var score = new QuantScoringEngine().Score(
            ticker: "ASII.JK",
            currentYield: 0.0832m,
            median5YYield: 0.0829m,
            payoutRatio: 48.1m,
            fcfPayoutRatio: 57.9m,
            dividendHistoryScore: 1,
            epsCagr5Y: null,
            epsCagr3Y: 4.2m,
            earningsConsistencyScore: 8.3m,
            peValuationScore: 8m,
            yieldValuationScore: 7.6m,
            fcfValuationScore: 5m,
            riskScore: 11m);

        // Quant status measures overall quality/score thresholds.
        // BuyZone is a separate price-vs-fair-value decision layer.
        Assert.Equal(72.9m, score.TotalScore);
        Assert.Equal(AnalysisStatus.Watch, score.Status);
    }
}
