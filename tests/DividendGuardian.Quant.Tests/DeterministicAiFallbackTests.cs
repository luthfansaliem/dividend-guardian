using DividendGuardian.AI;
using DividendGuardian.Domain;
using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class DeterministicAiFallbackTests
{
    [Fact]
    public void Create_PreservesQuantDecisionAndNeverInventsAiFacts()
    {
        var score = new QuantScore(
            "ASII", 10m, 20m, 15m, 20m, 10m, 75m, AnalysisStatus.Watch);

        var zone = new BuyZone(
            100m, 110m, 120m, 0.10m, "ACCUMULATE", Array.Empty<string>());

        var quant = new QuantAnalysisResult(
            score,
            new DividendQualityMetrics(null, null, null, null, null, 0, 0),
            90m, 3m, 2m, 10m, 11m, 12m, 10m, 10m,
            new FairValueRange(100m, 110m, 120m),
            0.10m,
            zone,
            Array.Empty<string>(),
            "PARTIAL");

        var request = new AiAnalysisRequest(
            "ASII",
            new DateOnly(2026, 9, 25),
            quant,
            Array.Empty<AiTriggerEvent>());

        var result = DeterministicAiFallback.Create(request, "API timeout.");

        Assert.Equal("ASII", result.Ticker);
        Assert.Equal("INSUFFICIENT DATA", result.Verdict);
        Assert.Equal("PARTIAL", result.DataQuality);
        Assert.Equal("deterministic-fallback", result.Model);
        Assert.Contains("API timeout.", result.DataGaps);
        Assert.Contains("75.0/100", result.WhyAccumulate);
        Assert.Contains("10.0%", result.WhyAccumulate);
    }
}
