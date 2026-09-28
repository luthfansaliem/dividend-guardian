using System.Collections.Concurrent;
using System.Text.Json;

namespace DividendGuardian.Infrastructure;

public sealed class YahooFinanceFxRateProvider(HttpClient httpClient) : IFxRateProvider
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<int, decimal>> cache = new();

    public async Task<IReadOnlyDictionary<int, decimal>> GetYearEndRatesAsync(
        string fromCurrency, string toCurrency, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (fromCurrency.Equals(toCurrency, StringComparison.OrdinalIgnoreCase))
            return Enumerable.Range(from.Year, to.Year - from.Year + 1).ToDictionary(y => y, _ => 1m);

        var source = fromCurrency.ToUpperInvariant();
        var target = toCurrency.ToUpperInvariant();
        var key = source + target + ":" + from.Year + ":" + to.Year;
        if (cache.TryGetValue(key, out var cached)) return cached;

        var symbol = source + target + "=X";
        var p1 = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var p2 = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var uri = "v8/finance/chart/" + Uri.EscapeDataString(symbol) +
                  "?period1=" + p1 + "&period2=" + p2 + "&interval=1d&events=history";

        using var response = await httpClient.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var result = json.RootElement.GetProperty("chart").GetProperty("result")[0];
        var timestamps = result.GetProperty("timestamp");
        var closes = result.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close");
        var latest = new Dictionary<int, (long Timestamp, decimal Rate)>();

        for (var i = 0; i < Math.Min(timestamps.GetArrayLength(), closes.GetArrayLength()); i++)
        {
            if (closes[i].ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
            var rate = closes[i].GetDecimal();
            if (rate <= 0) continue;
            var timestamp = timestamps[i].GetInt64();
            var year = DateTimeOffset.FromUnixTimeSeconds(timestamp).Year;
            if (!latest.TryGetValue(year, out var prior) || timestamp > prior.Timestamp)
                latest[year] = (timestamp, rate);
        }

        var rates = latest.ToDictionary(x => x.Key, x => x.Value.Rate);
        cache[key] = rates;
        return rates;
    }
}
