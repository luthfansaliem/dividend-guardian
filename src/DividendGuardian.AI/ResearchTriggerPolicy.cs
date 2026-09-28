using DividendGuardian.Domain;
using DividendGuardian.Quant;

namespace DividendGuardian.AI;

public sealed class ResearchTriggerPolicy
{
    public ResearchDecision Evaluate(
        QuantAnalysisResult current,
        ResearchTriggerState? previous = null,
        bool force = false,
        decimal materialScoreChange = 5m)
    {
        var triggers = new List<ResearchTrigger>();

        if (force)
            triggers.Add(new(ResearchTriggerKind.Manual, "Research was manually forced."));

        if (current.Score.Status == AnalysisStatus.Accumulate)
            triggers.Add(new(ResearchTriggerKind.AccumulateCandidate,
                "Quant status is Accumulate; external evidence should challenge or support the thesis."));

        var inBuyZone = IsAccumulationZone(current.BuyZone.Status);
        var wasInBuyZone = previous is not null && IsAccumulationZone(previous.BuyZoneStatus);
        if (inBuyZone && !wasInBuyZone)
            triggers.Add(new(ResearchTriggerKind.EnteredBuyZone,
                $"Price entered Quant buy zone ({current.BuyZone.Status})."));

        if (previous is not null &&
            !string.Equals(current.Score.Status.ToString(), previous.QuantStatus, StringComparison.OrdinalIgnoreCase))
            triggers.Add(new(ResearchTriggerKind.QuantStatusChanged,
                $"Quant status changed from {previous.QuantStatus} to {current.Score.Status}."));

        if (previous is not null)
        {
            var change = Math.Abs(current.Score.TotalScore - previous.TotalScore);
            if (change >= materialScoreChange)
                triggers.Add(new(ResearchTriggerKind.QuantScoreChanged,
                    $"Quant score changed by {change:F1} points."));
        }

        return new ResearchDecision(triggers.Count > 0, triggers);
    }

    private static bool IsAccumulationZone(string status) =>
        status is "STRONG_ACCUMULATE" or "ACCUMULATE";
}
