using DividendGuardian.Quant;

namespace DividendGuardian.AI;

public static class DeterministicAiFallback
{
    public static AiAnalysisResponse Create(
        AiAnalysisRequest request,
        string reason)
    {
        var quant = request.Quant;
        var risks = new List<string>
        {
            "AI analysis unavailable; review the deterministic Quant output directly."
        };

        if (quant.DataQuality != "READY")
            risks.Add($"Data quality is {quant.DataQuality}.");

        if (quant.MarginOfSafety is < 0)
            risks.Add("Current price is above conservative fair value.");

        return new AiAnalysisResponse(
            request.Ticker,
            quant.DataQuality == "READY" ? quant.BuyZone.Status : "INSUFFICIENT DATA",
            BuildWhyAccumulate(quant),
            BuildWhyNotAccumulate(quant),
            risks,
            new[] { reason },
            new[] { "Re-run AI analysis after the configured AI service becomes available." },
            quant.DataQuality,
            "deterministic-fallback",
            "DG-AI-FALLBACK-1.0");
    }

    private static string BuildWhyAccumulate(QuantAnalysisResult quant) =>
        $"Deterministic Quant status is {quant.BuyZone.Status}; " +
        $"Quant score is {quant.Score.TotalScore:F1}/100 and " +
        $"margin of safety is {(quant.MarginOfSafety is null ? "unavailable" : quant.MarginOfSafety.Value.ToString("P1"))}. " +
        "No additional AI interpretation is available.";

    private static string BuildWhyNotAccumulate(QuantAnalysisResult quant) =>
        $"Deterministic Quant status is {quant.BuyZone.Status}. " +
        $"Data quality is {quant.DataQuality}. " +
        "This fallback does not infer missing facts or override the Quant result.";
}
