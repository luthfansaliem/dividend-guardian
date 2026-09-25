using DividendGuardian.Quant;
using Microsoft.Extensions.Logging;

namespace DividendGuardian.Infrastructure;

public sealed class QuantAnalysisOrchestrator(
    StockRepository stocks,
    QuantDataRepository data,
    QuantAnalysisRepository repository,
    QuantAnalysisEngine engine,
    ILogger<QuantAnalysisOrchestrator> logger)
{
    public async Task<QuantAnalysisCycleResult> AnalyzeWatchlistAsync(
        DateOnly analysisDate,
        CancellationToken ct = default)
    {
        var watchlist = await stocks.GetActiveWatchlistAsync(ct);
        var success = 0;
        var insufficient = 0;
        var failed = 0;
        var results = new List<QuantAnalysisItem>();

        logger.LogInformation(
            "Quant analysis cycle started. Date={AnalysisDate}, Stocks={Count}",
            analysisDate, watchlist.Count);

        foreach (var stock in watchlist)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var input = await data.LoadAsync(stock.Ticker, analysisDate, ct);
                if (input is null)
                {
                    insufficient++;
                    await repository.RecordInsufficientDataAsync(stock.Ticker, analysisDate, ct);
                    logger.LogWarning("Quant analysis skipped for {Ticker}: insufficient data.", stock.Ticker);
                    continue;
                }

                var result = engine.Analyze(input);
                await repository.SaveAsync(result, analysisDate, ct);
                results.Add(new QuantAnalysisItem(stock.Ticker, result));

                success++;
                logger.LogInformation(
                    "Quant analysis {Ticker}: score={Score:F1}, status={Status}",
                    stock.Ticker, result.Score.TotalScore, result.Score.Status);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                await repository.RecordFailureAsync(stock.Ticker, analysisDate, ex.Message, ct);
                logger.LogError(ex, "Quant analysis failed for {Ticker}", stock.Ticker);
            }
        }

        logger.LogInformation(
            "Quant analysis cycle completed. Success={Success}, Insufficient={Insufficient}, Failed={Failed}",
            success, insufficient, failed);

        return new QuantAnalysisCycleResult(success, insufficient, failed, results);
    }
}

public sealed record QuantAnalysisItem(
    string Ticker,
    QuantAnalysisResult Result);

public sealed record QuantAnalysisCycleResult(
    int Success,
    int Insufficient,
    int Failed,
    IReadOnlyList<QuantAnalysisItem>? Results = null);
