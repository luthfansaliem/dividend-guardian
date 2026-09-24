using DividendGuardian.Quant;

namespace DividendGuardian.AI;

public sealed class AiTriggerPolicy
{
    public bool ShouldAnalyze(QuantAnalysisResult current, QuantAnalysisResult? previous, IReadOnlyCollection<AiTriggerEvent> externalTriggers)
    {
        if (externalTriggers.Count > 0) return true;
        if (current.BuyZone.Status is "STRONG_ACCUMULATE" or "ACCUMULATE") return true;
        if (previous is null) return false;
        var priceDrop = previous.CurrentPrice > 0 ? 1m - current.CurrentPrice / previous.CurrentPrice : 0m;
        if (priceDrop >= 0.05m) return true;
        return Math.Abs(current.Score.TotalScore - previous.Score.TotalScore) >= 5m;
    }
}
