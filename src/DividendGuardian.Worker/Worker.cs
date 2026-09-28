using DividendGuardian.Infrastructure;
using DividendGuardian.Notification;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    Database database,
    MarketDataCollector marketData,
    FundamentalCollector fundamentals,
    QuantAnalysisOrchestrator quantAnalysis,
    AiAnalysisCycleOrchestrator aiAnalysis,
    IOptions<WorkerOptions> options,
    IOptions<MarketDataOptions> marketOptions,
    IOptions<FundamentalDataOptions> fundamentalOptions,
    TelegramAlertService telegram) : BackgroundService
{
    private DateTimeOffset? lastFundamentalRun;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Dividend Guardian started. Environment={Environment}", options.Value.Environment);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ping = await database.PingAsync(stoppingToken);
                logger.LogInformation("Database OK={Ping}", ping);

                if (!marketOptions.Value.Provider.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    var to = DateOnly.FromDateTime(DateTime.UtcNow);
                    var from = to.AddDays(-Math.Max(1, marketOptions.Value.LookbackDays));
                    var rows = await marketData.CollectAsync(from, to, stoppingToken);
                    logger.LogInformation("Market data cycle complete. Rows written={Rows}", rows);
                }

                if (!fundamentalOptions.Value.Provider.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                    (lastFundamentalRun is null ||
                     DateTimeOffset.UtcNow - lastFundamentalRun.Value >=
                     TimeSpan.FromDays(Math.Max(1, options.Value.FundamentalIntervalDays))))
                {
                    var to = DateOnly.FromDateTime(DateTime.UtcNow);
                    var from = to.AddYears(-Math.Max(1, fundamentalOptions.Value.LookbackYears));
                    var rows = await fundamentals.CollectAsync(from, to, stoppingToken);
                    lastFundamentalRun = DateTimeOffset.UtcNow;
                    logger.LogInformation("Fundamental data cycle complete. Rows written={Rows}", rows);
                }

                var analysisDate = DateOnly.FromDateTime(DateTime.UtcNow);
                var analysis = await quantAnalysis.AnalyzeWatchlistAsync(analysisDate, stoppingToken);
                logger.LogInformation(
                    "Quant analysis cycle complete. Success={Success}, Insufficient={Insufficient}, Failed={Failed}",
                    analysis.Success, analysis.Insufficient, analysis.Failed);

                var ai = await aiAnalysis.AnalyzeAsync(
                    analysis.Results ?? Array.Empty<QuantAnalysisItem>(),
                    analysisDate,
                    stoppingToken);

                logger.LogInformation(
                    "AI analysis cycle complete. Analyzed={Analyzed}, Skipped={Skipped}, Failed={Failed}",
                    ai.Analyzed, ai.Skipped, ai.Failed);

                foreach (var item in ai.Items)
                {
                    try
                    {
                        var alert = new TelegramAlertData(
                            item.RunId,
                            item.Response.Ticker,
                            item.Response.Verdict,
                            item.Quant.Score.TotalScore,
                            item.Quant.CurrentPrice,
                            item.Quant.FairValue.Conservative,
                            item.Quant.FairValue.Base,
                            item.Quant.MarginOfSafety,
                            item.Quant.DataQuality,
                            item.Response.WhyAccumulate,
                            item.Response.WhyNotAccumulate,
                            item.Response.KeyRisks,
                            item.Response.InvalidationTriggers,
                            item.Response.DataGaps,
                            item.UsedFallback);

                        await telegram.SendAsync(alert, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Telegram notification failed for {Ticker}", item.Ticker);
                    }
                }

                if (marketOptions.Value.Provider.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                    fundamentalOptions.Value.Provider.Equals("none", StringComparison.OrdinalIgnoreCase))
                    logger.LogWarning("No data providers are configured.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled cycle failed.");
            }

            await Task.Delay(options.Value.Interval, stoppingToken);
        }
    }
}

public sealed class WorkerOptions
{
    public string Environment { get; set; } = "Development";
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);
    public int FundamentalIntervalDays { get; set; } = 7;
}
