using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Infrastructure;

public sealed class MarketDataCollector(
    IMarketDataProvider provider,
    StockRepository stocks,
    MarketDataRepository repository,
    IOptions<MarketDataOptions> options,
    ILogger<MarketDataCollector> logger)
{
    public async Task<int> CollectAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var watchlist = await stocks.GetActiveWatchlistAsync(ct);
        var total = 0;
        foreach (var stock in watchlist)
        {
            try
            {
                var raw = await provider.GetEodPricesAsync(stock.Ticker, from, to, ct);
                var valid = MarketDataValidator.ValidateAndNormalize(stock.Ticker, raw);
                await repository.UpsertEodPricesAsync(valid, ct);
                await repository.RecordIngestionAsync(options.Value.Provider, stock.Ticker, from, to, valid.Count, "SUCCESS", null, ct);
                total += valid.Count;
                logger.LogInformation("Market data {Ticker}: {Rows} rows", stock.Ticker, valid.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await repository.RecordIngestionAsync(options.Value.Provider, stock.Ticker, from, to, 0, "FAILED", ex.Message, ct);
                logger.LogError(ex, "Market data failed for {Ticker}", stock.Ticker);
            }
        }
        return total;
    }
}
