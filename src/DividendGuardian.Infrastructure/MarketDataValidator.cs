namespace DividendGuardian.Infrastructure;

public static class MarketDataValidator
{
    public static IReadOnlyList<EodPrice> ValidateAndNormalize(string expectedTicker, IEnumerable<EodPrice> prices)
    {
        var ticker = expectedTicker.Trim().ToUpperInvariant();
        var result = prices.Where(x => x.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.TradeDate).GroupBy(x => x.TradeDate).Select(g => g.Last()).ToArray();
        foreach (var p in result)
        {
            if (p.TradeDate == default) throw new InvalidDataException($"{ticker}: invalid trade date.");
            if (p.Open <= 0 || p.High <= 0 || p.Low <= 0 || p.Close <= 0) throw new InvalidDataException($"{ticker} {p.TradeDate}: OHLC must be positive.");
            if (p.High < p.Low || p.High < p.Open || p.High < p.Close || p.Low > p.Open || p.Low > p.Close) throw new InvalidDataException($"{ticker} {p.TradeDate}: invalid OHLC relationship.");
            if (p.Volume < 0) throw new InvalidDataException($"{ticker} {p.TradeDate}: volume cannot be negative.");
        }
        return result;
    }
}
