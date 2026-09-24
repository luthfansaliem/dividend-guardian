using DividendGuardian.Infrastructure;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Worker;

public sealed class Worker(ILogger<Worker> logger,Database database,StockRepository stocks,IOptions<WorkerOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Dividend Guardian started. Environment={Environment}",options.Value.Environment);
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ping=await database.PingAsync(stoppingToken);
                var watchlist=await stocks.GetActiveWatchlistAsync(stoppingToken);
                logger.LogInformation("Database OK={Ping}; Active watchlist={Count}",ping,watchlist.Count);
            }
            catch(Exception ex){ logger.LogError(ex,"Daily cycle failed."); }
            await Task.Delay(options.Value.Interval,stoppingToken);
        }
    }
}

public sealed class WorkerOptions
{
    public string Environment { get; set; }="Development";
    public TimeSpan Interval { get; set; }=TimeSpan.FromHours(24);
}