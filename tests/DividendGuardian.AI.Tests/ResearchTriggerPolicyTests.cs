using DividendGuardian.AI;
using DividendGuardian.Domain;
using DividendGuardian.Quant;

namespace DividendGuardian.AI.Tests;

public sealed class ResearchTriggerPolicyTests
{
    [Fact]
    public void AccumulateCandidate_TriggersResearch()
    {
        var result = CreateResult(80m, AnalysisStatus.Accumulate, "REVIEW");
        var decision = new ResearchTriggerPolicy().Evaluate(result);

        Assert.True(decision.ShouldResearch);
        Assert.Contains(decision.Triggers, x => x.Kind == ResearchTriggerKind.AccumulateCandidate);
    }

    [Fact]
    public void StableWatch_DoesNotTriggerResearch()
    {
        var result = CreateResult(70m, AnalysisStatus.Watch, "REVIEW");
        var previous = new ResearchTriggerState(69m, "Watch", "REVIEW");

        Assert.False(new ResearchTriggerPolicy().Evaluate(result, previous).ShouldResearch);
    }

    [Fact]
    public void EnteringBuyZone_TriggersResearch()
    {
        var result = CreateResult(70m, AnalysisStatus.Watch, "ACCUMULATE");
        var previous = new ResearchTriggerState(70m, "Watch", "REVIEW");

        var decision = new ResearchTriggerPolicy().Evaluate(result, previous);

        Assert.Contains(decision.Triggers, x => x.Kind == ResearchTriggerKind.EnteredBuyZone);
    }

    [Fact]
    public void MaterialScoreChange_TriggersResearch()
    {
        var result = CreateResult(72m, AnalysisStatus.Watch, "REVIEW");
        var previous = new ResearchTriggerState(65m, "Watch", "REVIEW");

        var decision = new ResearchTriggerPolicy().Evaluate(result, previous);

        Assert.Contains(decision.Triggers, x => x.Kind == ResearchTriggerKind.QuantScoreChanged);
    }

    [Fact]
    public void Force_AlwaysTriggersResearch()
    {
        var result = CreateResult(40m, AnalysisStatus.Avoid, "AVOID");

        var decision = new ResearchTriggerPolicy().Evaluate(result, force: true);

        Assert.Contains(decision.Triggers, x => x.Kind == ResearchTriggerKind.Manual);
    }

    private static QuantAnalysisResult CreateResult(decimal score, AnalysisStatus status, string buyZone)
    {
        var quantScore = new QuantScore("TEST", 0, 0, 0, 0, 0, score, status);
        var metrics = new DividendQualityMetrics(null, null, null, null, null, 0, 0);
        var fairValue = new FairValueRange(null, null, null);
        var confidence = new ValuationConfidenceMetrics(0, null, null, null, "UNKNOWN");
        var zone = new BuyZone(buyZone, null);

        return new QuantAnalysisResult(
            quantScore, metrics, 100m, 0m, null, null, null, null, null, 0m,
            fairValue, confidence, null, zone, Array.Empty<string>(), "PARTIAL");
    }
}
