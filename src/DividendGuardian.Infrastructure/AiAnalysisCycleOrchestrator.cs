using DividendGuardian.AI;
using DividendGuardian.Quant;
using Microsoft.Extensions.Logging;

namespace DividendGuardian.Infrastructure;

public sealed class AiAnalysisCycleOrchestrator(
    AiAnalysisOrchestrator orchestrator,
    AiTriggerPolicy triggerPolicy,
    AiAnalysisRepository repository,
    QuantAnalysisRepository quantRepository,
    ILogger<AiAnalysisCycleOrchestrator> logger)
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);

    public async Task<AiAnalysisCycleResult> AnalyzeAsync(
        IReadOnlyList<QuantAnalysisItem> quantResults,
        DateOnly analysisDate,
        CancellationToken ct = default)
    {
        var analyzed = 0;
        var skipped = 0;
        var failed = 0;
        var analysisItems = new List<AiAnalysisCycleItem>();

        foreach (var item in quantResults)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var lastAnalysisAt = await repository.GetLatestAnalysisAtAsync(item.Ticker, ct);
                var previous = await quantRepository.GetPreviousTriggerStateAsync(
                    item.Ticker,
                    analysisDate,
                    ct);
                var occurredAt = DateTimeOffset.UtcNow;

                var triggers = triggerPolicy.EvaluateTriggers(
                    item.Result,
                    previous,
                    occurredAt);

                if (!triggerPolicy.ShouldAnalyzeFromState(
                        item.Result,
                        previous,
                        triggers,
                        lastAnalysisAt,
                        occurredAt,
                        Cooldown))
                {
                    skipped++;
                    logger.LogDebug(
                        "AI analysis skipped for {Ticker}: no trigger or inside cooldown.",
                        item.Ticker);
                    continue;
                }

                var request = new AiAnalysisRequest(
                    item.Ticker,
                    analysisDate,
                    item.Result,
                    triggers);

                var result = await orchestrator.AnalyzeAsync(request, ct);

                await repository.SaveAsync(
                    result.Response,
                    request.Triggers,
                    result.UsedFallback,
                    result.FailureReason,
                    ct);

                analyzed++;
                analysisItems.Add(new AiAnalysisCycleItem(
                    item.Ticker,
                    item.Result,
                    result.Response,
                    result.UsedFallback));

                if (result.UsedFallback)
                {
                    logger.LogWarning(
                        "AI analysis fallback for {Ticker}: {Reason}",
                        item.Ticker,
                        result.FailureReason);
                }
                else
                {
                    logger.LogInformation(
                        "AI analysis completed for {Ticker}. Model={Model}",
                        item.Ticker,
                        result.Response.Model);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "AI analysis cycle failed for {Ticker}", item.Ticker);
            }
        }

        return new AiAnalysisCycleResult(analyzed, skipped, failed, analysisItems);
    }
}

public sealed record AiAnalysisCycleItem(
    string Ticker,
    QuantAnalysisResult Quant,
    AiAnalysisResponse Response,
    bool UsedFallback);

public sealed record AiAnalysisCycleResult(
    int Analyzed,
    int Skipped,
    int Failed,
    IReadOnlyList<AiAnalysisCycleItem> Items);
