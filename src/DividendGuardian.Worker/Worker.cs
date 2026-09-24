using DividendGuardian.Infrastructure;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    Database database,
    MarketDataCollector marketData,
    IOptions<WorkerOptions> options,
    IOptions<MarketDataOptions> marketOptions) : BackgroundService
{
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
                else
                {
                    logger.LogWarning("Market data provider is not configured. Set MARKET_DATA_PROVIDER.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Daily cycle failed."); }

            await Task.Delay(options.Value.Interval, stoppingToken);
        }
    }
}

public sealed class WorkerOptions
{
    public string Environment { get; set; } = "Development";
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);
}
