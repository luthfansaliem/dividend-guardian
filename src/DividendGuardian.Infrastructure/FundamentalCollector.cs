using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Infrastructure;

public sealed class FundamentalCollector(
    StockRepository stocks, IFundamentalDataProvider provider, FundamentalDataRepository repository,
    IOptions<FundamentalDataOptions> options, ILogger<FundamentalCollector> logger)
{
    public async Task<int> CollectAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var total = 0;
        var watchlist = await stocks.GetActiveWatchlistAsync(ct);
        foreach (var stock in watchlist)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fundamentals = await provider.GetAnnualFundamentalsAsync(stock.Ticker, from, to, ct);
                var dividends = options.Value.IncludeDividends ? await provider.GetDividendsAsync(stock.Ticker, from, to, ct) : [];
                await repository.UpsertFundamentalsAsync(fundamentals, ct);
                await repository.UpsertDividendsAsync(dividends, ct);
                await repository.RecordIngestionAsync(options.Value.Provider, stock.Ticker, from, to, fundamentals.Count, dividends.Count, "SUCCESS", null, ct);
                total += fundamentals.Count;
                logger.LogInformation("Fundamental data collected for {Ticker}: fundamentals={Fundamentals}, dividends={Dividends}", stock.Ticker, fundamentals.Count, dividends.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                await repository.RecordIngestionAsync(options.Value.Provider, stock.Ticker, from, to, 0, 0, "FAILED", ex.Message, ct);
                logger.LogError(ex, "Fundamental collection failed for {Ticker}", stock.Ticker);
            }
        }
        return total;
    }
}