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
}