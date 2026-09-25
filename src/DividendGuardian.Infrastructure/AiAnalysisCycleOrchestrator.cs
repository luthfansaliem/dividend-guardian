using DividendGuardian.AI;
using DividendGuardian.Quant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Infrastructure;

public sealed class AiAnalysisCycleOrchestrator(
    AiAnalysisOrchestrator orchestrator,
    AiTriggerPolicy triggerPolicy,
    AiAnalysisRepository repository,
    QuantAnalysisRepository quantRepository,
    IOptions<AiOptions> aiOptions,
    ILogger<AiAnalysisCycleOrchestrator> logger)
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);

    public async Task<AiAnalysisCycleResult> AnalyzeAsync(
        IReadOnlyList<QuantAnalysisItem> quantResults,
        DateOnly analysisDate,
        CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        var analyzed = 0;
        var skipped = 0;
        var failed = 0;
        var analysisItems = new List<AiAnalysisCycleItem>();
        var dailyLimit = Math.Max(0, aiOptions.Value.MaxAnalysesPerDay);
        var dayStart = GetJakartaDayStartUtc(analysisDate);
        var usedToday = await repository.GetAnalysisCountSinceAsync(dayStart, ct);

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["RunId"] = runId,
            ["AnalysisDate"] = analysisDate
        });

        logger.LogInformation(
            "AI analysis cycle started. RunId={RunId}, Tickers={TickerCount}, DailyBudget={DailyLimit}, UsedToday={UsedToday}",
            runId, quantResults.Count, dailyLimit, usedToday);

        foreach (var item in quantResults)
        {
            ct.ThrowIfCancellationRequested();

            using var tickerScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["Ticker"] = item.Ticker
            });

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
                    logger.LogDebug("AI analysis skipped: no trigger or inside cooldown.");
                    continue;
                }

                if (usedToday >= dailyLimit)
                {
                    skipped++;
                    logger.LogWarning(
                        "AI analysis skipped: daily AI budget exhausted ({Used}/{Limit}).",
                        usedToday,
                        dailyLimit);
                    continue;
                }

                logger.LogInformation(
                    "AI analysis attempt started. TriggerCount={TriggerCount}",
                    triggers.Count);

                var request = new AiAnalysisRequest(
                    item.Ticker,
                    analysisDate,
                    item.Result,
                    triggers);

                var result = await orchestrator.AnalyzeAsync(request, ct);
                var saved = await repository.SaveAsync(
                    runId,
                    result.Response,
                    request.Triggers,
                    result.UsedFallback,
                    result.FailureReason,
                    ct);

                if (saved)
                    usedToday++;

                if (!saved)
                {
                    skipped++;
                    logger.LogWarning("AI analysis result was already persisted; duplicate suppressed.");
                    continue;
                }

                analyzed++;
                analysisItems.Add(new AiAnalysisCycleItem(
                    runId,
                    item.Ticker,
                    item.Result,
                    result.Response,
                    result.UsedFallback));

                if (result.UsedFallback)
                {
                    logger.LogWarning(
                        "AI analysis fallback completed. Reason={Reason}",
                        result.FailureReason);
                }
                else
                {
                    logger.LogInformation(
                        "AI analysis completed. Model={Model}",
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
                logger.LogError(ex, "AI analysis cycle failed.");
            }
        }

        logger.LogInformation(
            "AI analysis cycle completed. RunId={RunId}, Analyzed={Analyzed}, Skipped={Skipped}, Failed={Failed}",
            runId, analyzed, skipped, failed);

        return new AiAnalysisCycleResult(analyzed, skipped, failed, analysisItems);
    }

    private static DateTimeOffset GetJakartaDayStartUtc(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var timeZone = GetJakartaTimeZone();
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        return new DateTimeOffset(utcStart);
    }

    private static TimeZoneInfo GetJakartaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}

public sealed record AiAnalysisCycleItem(
    Guid RunId,
    string Ticker,
    QuantAnalysisResult Quant,
    AiAnalysisResponse Response,
    bool UsedFallback);

public sealed record AiAnalysisCycleResult(
    int Analyzed,
    int Skipped,
    int Failed,
    IReadOnlyList<AiAnalysisCycleItem> Items);
