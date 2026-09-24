using System.Collections.Generic;

namespace DividendGuardian.Quant;

public sealed record BuyZone(
    decimal? ConservativeFairValue,
    decimal? BaseFairValue,
    decimal? OptimisticFairValue,
    decimal? MarginOfSafety,
    string Status,
    IReadOnlyList<string> Reasons);

public sealed class BuyZoneEngine
{
    public BuyZone Evaluate(
        decimal currentPrice,
        decimal? conservative,
        decimal? baseValue,
        decimal? optimistic,
        decimal quantScore)
    {
        if (currentPrice <= 0)
            return Review(conservative, baseValue, optimistic, "Current price is unavailable or invalid.");

        if (conservative is null || conservative <= 0)
            return Review(conservative, baseValue, optimistic, "Conservative fair value is unavailable; accumulation status cannot be determined.");

        var marginOfSafety = 1m - currentPrice / conservative.Value;
        var status = marginOfSafety >= 0.20m && quantScore >= 80m
            ? "STRONG_ACCUMULATE"
            : marginOfSafety >= 0.10m && quantScore >= 70m
                ? "ACCUMULATE"
                : marginOfSafety >= 0m
                    ? "WATCH"
                    : "REVIEW";

        var reasons = new List<string>
        {
            $"Current price {currentPrice:F2}; conservative fair value {conservative.Value:F2}.",
            $"Margin of safety {marginOfSafety:P1}.",
            $"Quant score {quantScore:F1}/100.",
            $"Accumulation status {status}."
        };

        if (status == "REVIEW")
            reasons.Add("Current price is above the conservative fair value.");
        else if (status == "WATCH")
            reasons.Add("Price is not above conservative fair value, but the quant score and/or margin of safety do not meet accumulation thresholds.");
        else
            reasons.Add("Price and quant quality both meet the configured accumulation thresholds.");

        return new BuyZone(
            conservative,
            baseValue,
            optimistic,
            marginOfSafety,
            status,
            reasons);
    }

    private static BuyZone Review(
        decimal? conservative,
        decimal? baseValue,
        decimal? optimistic,
        string reason) =>
        new(
            conservative,
            baseValue,
            optimistic,
            null,
            "REVIEW",
            new[] { reason, "No accumulation status is assigned without a valid conservative fair value." });
}
