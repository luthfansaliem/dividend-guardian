using DividendGuardian.Quant;

namespace DividendGuardian.AI;

public enum ResearchTriggerKind
{
    AccumulateCandidate,
    EnteredBuyZone,
    QuantStatusChanged,
    QuantScoreChanged,
    Manual
}

public sealed record ResearchTrigger(
    ResearchTriggerKind Kind,
    string Reason);

public sealed record ResearchTriggerState(
    decimal TotalScore,
    string QuantStatus,
    string BuyZoneStatus);

public sealed record ResearchDecision(
    bool ShouldResearch,
    IReadOnlyList<ResearchTrigger> Triggers);
