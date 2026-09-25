using DividendGuardian.Quant;

namespace DividendGuardian.AI;

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
