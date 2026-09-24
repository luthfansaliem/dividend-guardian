using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Infrastructure;

public sealed class TwelveDataMarketDataProvider(
    HttpClient httpClient,
    IOptions<MarketDataOptions> options) : IMarketDataProvider
{
    private readonly MarketDataOptions settings = options.Value;

    public async Task<IReadOnlyList<EodPrice>> GetEodPricesAsync(
        string ticker, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("TWELVE_DATA_API_KEY is not configured.");

        var uri = $"{settings.BaseUrl.TrimEnd('/')}/time_series" +
                  $"?symbol={Uri.EscapeDataString(ticker)}" +
                  $"&mic_code={Uri.EscapeDataString(settings.MicCode)}" +
                  $"&interval=1day" +
                  $"&start_date={from:yyyy-MM-dd}" +
                  $"&end_date={to:yyyy-MM-dd}" +
                  $"&outputsize=5000" +
                  $"&apikey={Uri.EscapeDataString(settings.ApiKey)}";

        using var response = await httpClient.GetAsync(uri, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Twelve Data HTTP {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);
        if (json.RootElement.TryGetProperty("code", out var code))
        {
            var message = json.RootElement.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : "Unknown provider error";
            throw new InvalidOperationException($"Twelve Data error {code}: {message}");
        }

        if (!json.RootElement.TryGetProperty("values", out var values) ||
            values.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<EodPrice>();
        foreach (var item in values.EnumerateArray())
        {
            var date = DateOnly.ParseExact(item.GetProperty("datetime").GetString()!,
                "yyyy-MM-dd", CultureInfo.InvariantCulture);

            result.Add(new EodPrice(
                ticker.ToUpperInvariant(),
                date,
                ParseDecimal(item, "open"),
                ParseDecimal(item, "high"),
                ParseDecimal(item, "low"),
                ParseDecimal(item, "close"),
                ParseLong(item, "volume")));
        }

        return result.OrderBy(x => x.TradeDate).ToArray();
    }

    private static decimal ParseDecimal(JsonElement item, string property) =>
        decimal.Parse(item.GetProperty(property).GetString()!, CultureInfo.InvariantCulture);

    private static long ParseLong(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) &&
        value.ValueKind != JsonValueKind.Null &&
        long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
}
