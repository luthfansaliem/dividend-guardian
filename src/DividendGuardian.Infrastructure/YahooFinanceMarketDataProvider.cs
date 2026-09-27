using System.Globalization;
using System.Text.Json;

namespace DividendGuardian.Infrastructure;

public sealed class YahooFinanceMarketDataProvider(
    HttpClient httpClient) : IMarketDataProvider
{
    public async Task<IReadOnlyList<EodPrice>> GetEodPricesAsync(
        string ticker,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            throw new ArgumentException("Ticker is required.", nameof(ticker));

        if (from > to)
            throw new ArgumentException("'from' must be before or equal to 'to'.");

        var period1 = new DateTimeOffset(
            from.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero).ToUnixTimeSeconds();

        var period2 = new DateTimeOffset(
            to.AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero).ToUnixTimeSeconds();

        var symbol = Uri.EscapeDataString(ticker.ToUpperInvariant());

        var uri =
            $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}" +
            $"?period1={period1}" +
            $"&period2={period2}" +
            "&interval=1d" +
            "&events=history" +
            "&includeAdjustedClose=true";

        using var response = await httpClient.GetAsync(uri, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Yahoo Finance HTTP {(int)response.StatusCode}: {body}");
        }

        using var json = JsonDocument.Parse(body);
        var chart = json.RootElement.GetProperty("chart");

        if (chart.TryGetProperty("error", out var error) &&
            error.ValueKind != JsonValueKind.Null)
        {
            var description =
                error.TryGetProperty("description", out var desc)
                    ? desc.GetString()
                    : "Unknown Yahoo Finance error";

            throw new InvalidOperationException(
                $"Yahoo Finance error: {description}");
        }

        var result = chart.GetProperty("result");

        if (result.ValueKind != JsonValueKind.Array ||
            result.GetArrayLength() == 0)
        {
            return [];
        }

        var first = result[0];
        var timestamps = first.GetProperty("timestamp");
        var quote = first.GetProperty("indicators").GetProperty("quote")[0];

        var opens = quote.GetProperty("open");
        var highs = quote.GetProperty("high");
        var lows = quote.GetProperty("low");
        var closes = quote.GetProperty("close");
        var volumes = quote.GetProperty("volume");

        var prices = new List<EodPrice>();

        for (var i = 0; i < timestamps.GetArrayLength(); i++)
        {
            var timestamp = timestamps[i].GetInt64();
            var date = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.Date);

            if (date < from || date > to)
                continue;

            if (!TryGetDecimal(opens[i], out var open) ||
                !TryGetDecimal(highs[i], out var high) ||
                !TryGetDecimal(lows[i], out var low) ||
                !TryGetDecimal(closes[i], out var close))
            {
                continue;
            }

            var volume = TryGetLong(volumes[i], out var parsedVolume)
                ? parsedVolume
                : 0;

            prices.Add(new EodPrice(
                ticker.ToUpperInvariant(),
                date,
                open,
                high,
                low,
                close,
                volume));
        }

        return prices.OrderBy(x => x.TradeDate).ToArray();
    }

    private static bool TryGetDecimal(JsonElement value, out decimal result)
    {
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out result))
            return true;

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out result))
            return true;

        result = 0;
        return false;
    }

    private static bool TryGetLong(JsonElement value, out long result)
    {
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out result))
            return true;

        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result))
            return true;

        result = 0;
        return false;
    }
}
