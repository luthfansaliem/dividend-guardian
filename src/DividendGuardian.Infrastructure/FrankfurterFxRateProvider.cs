using System.Collections.Concurrent;
using System.Text.Json;

namespace DividendGuardian.Infrastructure;

public sealed class FrankfurterFxRateProvider(HttpClient httpClient) : IFxRateProvider
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

        var result = new Dictionary<int, decimal>();
        for (var year = from.Year; year <= to.Year; year++)
        {
            var date = new DateOnly(year, 12, 31);
            var uri = "v2/rate/" + Uri.EscapeDataString(source) + "/" + Uri.EscapeDataString(target) +
                      "?date=" + date.ToString("yyyy-MM-dd");

            using var response = await httpClient.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var rate = json.RootElement.GetProperty("rate").GetDecimal();
            if (rate > 0) result[year] = rate;
        }

        cache[key] = result;
        return result;
    }
}
