using DividendGuardian.Quant;

namespace DividendGuardian.AI;

public sealed record AiAnalysisRequest(string Ticker, DateOnly AnalysisDate, QuantAnalysisResult Quant, IReadOnlyList<AiTriggerEvent> Triggers);

public sealed record AiAnalysisResponse(string Ticker, string Verdict, string WhyAccumulate, string WhyNotAccumulate, IReadOnlyList<string> KeyRisks, IReadOnlyList<string> DataGaps, IReadOnlyList<string> InvalidationTriggers, string DataQuality, string Model, string PromptVersion);

public interface IAiAnalyst
{
    Task<AiAnalysisResponse> AnalyzeAsync(AiAnalysisRequest request, CancellationToken cancellationToken = default);
}
