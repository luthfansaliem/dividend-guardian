using DividendGuardian.Infrastructure;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    Database database,
    MarketDataCollector marketData,
    FundamentalCollector fundamentals,
    QuantAnalysisOrchestrator quantAnalysis,
    IOptions<WorkerOptions> options,
    IOptions<MarketDataOptions> marketOptions,
    IOptions<FundamentalDataOptions> fundamentalOptions) : BackgroundService
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
