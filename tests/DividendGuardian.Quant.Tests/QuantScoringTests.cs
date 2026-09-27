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
}