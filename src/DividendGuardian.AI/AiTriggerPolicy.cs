using DividendGuardian.Quant;

namespace DividendGuardian.AI;

public sealed record AiTriggerState(
    decimal CurrentPrice,
    decimal TotalScore,
    string Status);

public sealed class AiTriggerPolicy
{
    public bool ShouldAnalyze(
        QuantAnalysisResult current,
        QuantAnalysisResult? previous,
        IReadOnlyCollection<AiTriggerEvent> externalTriggers,
        DateTimeOffset? lastAnalysisAt = null,
        DateTimeOffset? now = null,
        TimeSpan? cooldown = null)
    {
        if (externalTriggers.Count > 0)
            return IsOutsideCooldown(lastAnalysisAt, now, cooldown);

        if (IsInsideCooldown(lastAnalysisAt, now, cooldown))
            return false;

        if (current.BuyZone.Status is "STRONG_ACCUMULATE" or "ACCUMULATE")
            return true;

        if (previous is null)
            return false;

        var priceDrop = previous.CurrentPrice > 0
            ? 1m - current.CurrentPrice / previous.CurrentPrice
            : 0m;

        if (priceDrop >= 0.05m)
            return true;

        return Math.Abs(current.Score.TotalScore - previous.Score.TotalScore) >= 5m;
    }

    public IReadOnlyList<AiTriggerEvent> EvaluateTriggers(
        QuantAnalysisResult current,
        AiTriggerState? previous,
        DateTimeOffset occurredAt)
    {
        var triggers = new List<AiTriggerEvent>();
        var currentInBuyZone = current.BuyZone.Status is "STRONG_ACCUMULATE" or "ACCUMULATE";
        var previousInBuyZone = previous?.Status is "STRONG_ACCUMULATE" or "ACCUMULATE";

        if (currentInBuyZone && !previousInBuyZone)
        {
            triggers.Add(new AiTriggerEvent(
                AiAnalysisTrigger.PriceEnteredBuyZone,
                occurredAt,
                "Quant status entered an accumulation state."));
        }

        if (previous is not null && previous.CurrentPrice > 0)
        {
            var priceDrop = 1m - current.CurrentPrice / previous.CurrentPrice;
            if (priceDrop >= 0.05m)
            {
                triggers.Add(new AiTriggerEvent(
                    AiAnalysisTrigger.PriceDrop,
                    occurredAt,
                    $"Price dropped {priceDrop:P1} versus the previous Quant snapshot."));
            }
        }

        if (previous is not null)
        {
            var scoreChange = Math.Abs(current.Score.TotalScore - previous.TotalScore);
            if (scoreChange >= 5m)
            {
                triggers.Add(new AiTriggerEvent(
                    AiAnalysisTrigger.QuantScoreChanged,
                    occurredAt,
                    $"Quant score changed by {scoreChange:F1} points."));
            }
        }

        return triggers;
    }

    private static bool IsInsideCooldown(
        DateTimeOffset? lastAnalysisAt,
        DateTimeOffset? now,
        TimeSpan? cooldown)
    {
        if (lastAnalysisAt is null || cooldown is null || cooldown <= TimeSpan.Zero)
            return false;

        var currentTime = now ?? DateTimeOffset.UtcNow;
        return currentTime - lastAnalysisAt.Value < cooldown.Value;
    }

    private static bool IsOutsideCooldown(
        DateTimeOffset? lastAnalysisAt,
        DateTimeOffset? now,
        TimeSpan? cooldown) =>
        !IsInsideCooldown(lastAnalysisAt, now, cooldown);
}
