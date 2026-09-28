using DividendGuardian.Quant;

namespace DividendGuardian.AI;

public sealed class AiAnalysisOrchestrator(
    IAiAnalyst analyst)
{
    public async Task<AiOrchestrationResult> AnalyzeAsync(
        AiAnalysisRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var response = await analyst.AnalyzeAsync(request, ct);
            ValidateSafety(response, request.Ticker);
            return new AiOrchestrationResult(response, UsedFallback: false, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var fallback = DeterministicAiFallback.Create(request, ex.Message);
            return new AiOrchestrationResult(fallback, UsedFallback: true, ex.Message);
        }
    }

    private static void ValidateSafety(AiAnalysisResponse response, string ticker)
    {
        if (!string.Equals(response.Ticker, ticker, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI response ticker does not match the request.");

        if (string.IsNullOrWhiteSpace(response.Verdict) ||
            string.IsNullOrWhiteSpace(response.WhyAccumulate) ||
            string.IsNullOrWhiteSpace(response.WhyNotAccumulate) ||
            string.IsNullOrWhiteSpace(response.DataQuality))
            throw new InvalidOperationException("AI response is missing required fields.");
    }
}

public sealed record AiOrchestrationResult(
    AiAnalysisResponse Response,
    bool UsedFallback,
    string? FailureReason);
