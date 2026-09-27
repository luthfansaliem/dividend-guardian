using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DividendGuardian.Infrastructure;

public sealed class YahooFinanceFundamentalDataProvider(
    HttpClient httpClient) : IFundamentalDataProvider
{
    private const string FundamentalsBaseUrl =
        "https://query1.finance.yahoo.com/ws/fundamentals-timeseries/v1/finance/timeseries";

    public async Task<IReadOnlyCollection<FundamentalRecord>> GetAnnualFundamentalsAsync(
        string ticker,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            throw new ArgumentException("Ticker is required.", nameof(ticker));

        if (from > to)
            throw new ArgumentException("'from' must be before or equal to 'to'.");

        var symbol = ticker.ToUpperInvariant();
        var period1 = new DateTimeOffset(
            from.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero).ToUnixTimeSeconds();
        var period2 = new DateTimeOffset(
            to.AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero).ToUnixTimeSeconds();

        var types = string.Join(",", new[]
        {
            "annualTotalRevenue",
            "annualNetIncome",
            "annualDilutedEPS",
            "annualOperatingCashFlow",
            "annualCapitalExpenditure",
            "annualFreeCashFlow",
            "annualCommonStockEquity",
            "annualTotalDebt",
            "annualCashCashEquivalentsAndShortTermInvestments",
            "annualDilutedAverageShares",
            "annualBasicAverageShares"
        });

        var url =
            $"{FundamentalsBaseUrl}/{Uri.EscapeDataString(symbol)}" +
            $"?symbol={Uri.EscapeDataString(symbol)}" +
            $"&type={Uri.EscapeDataString(types)}" +
            $"&period1={period1}" +
            $"&period2={period2}" +
            "&lang=en-US&region=US&padTimeSeries=true";

        using var request = CreateRequest(url);
        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Yahoo Finance fundamentals HTTP {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);

        var timeseries = json.RootElement
            .GetProperty("timeseries");

        if (timeseries.TryGetProperty("error", out var error) &&
            error.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException(
                $"Yahoo Finance fundamentals error: {error}");
        }

        var results = timeseries.GetProperty("result");
        if (results.ValueKind != JsonValueKind.Array)
            return [];

        var byDate = new Dictionary<DateOnly, FundamentalValues>();

        foreach (var result in results.EnumerateArray())
        {
            var seriesName = result
                .GetProperty("meta")
                .GetProperty("type")[0]
                .GetString();

            if (string.IsNullOrWhiteSpace(seriesName))
                continue;

            if (!result.TryGetProperty(seriesName, out var values) ||
                values.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var value in values.EnumerateArray())
            {
                if (!value.TryGetProperty("asOfDate", out var dateElement) ||
                    !DateOnly.TryParse(
                        dateElement.GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date) ||
                    date < from ||
                    date > to)
                    continue;

                if (!value.TryGetProperty("reportedValue", out var reported))
                    continue;

                var number = TryGetDecimal(reported, "raw");
                if (number is null)
                    continue;

                if (!byDate.TryGetValue(date, out var current))
                    current = new FundamentalValues();

                current.Set(seriesName, number.Value);
                byDate[date] = current;
            }
        }

        return byDate
            .Where(x =>
                x.Value.Revenue > 0 ||
                x.Value.NetIncome > 0 ||
                x.Value.Eps > 0)
            .OrderBy(x => x.Key)
            .Select(x => new FundamentalRecord(
                symbol,
                x.Key,
                x.Value.Revenue,
                x.Value.NetIncome,
                x.Value.Eps,
                x.Value.FreeCashFlow != 0
                    ? x.Value.FreeCashFlow
                    : x.Value.OperatingCashFlow + x.Value.CapitalExpenditure,
                x.Value.Equity,
                x.Value.Debt,
                x.Value.Cash,
                x.Value.Shares > 0 ? (long)x.Value.Shares : 0))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<DividendRecord>> GetDividendsAsync(
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
        var url =
            $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}" +
            $"?period1={period1}" +
            $"&period2={period2}" +
            "&interval=1d" +
            "&events=dividends";

        using var request = CreateRequest(url);
        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Yahoo Finance dividends HTTP {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);
        var chart = json.RootElement.GetProperty("chart");

        if (chart.TryGetProperty("error", out var error) &&
            error.ValueKind != JsonValueKind.Null)
        {
            var description = error.TryGetProperty("description", out var desc)
                ? desc.GetString()
                : "Unknown Yahoo Finance error";

            throw new InvalidOperationException(
                $"Yahoo Finance dividends error: {description}");
        }

        var result = chart.GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
            return [];

        var first = result[0];

        if (!first.TryGetProperty("events", out var events) ||
            !events.TryGetProperty("dividends", out var dividends) ||
            dividends.ValueKind != JsonValueKind.Object)
            return [];

        var records = new List<DividendRecord>();

        foreach (var dividend in dividends.EnumerateObject())
        {
            if (!dividend.Value.TryGetProperty("amount", out var amountElement) ||
                !TryGetDecimal(amountElement, out var amount) ||
                amount <= 0)
                continue;

            if (!long.TryParse(
                    dividend.Name,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var timestamp))
                continue;

            var date = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.Date);

            if (date < from || date > to)
                continue;

            // Yahoo exposes dividend cash events by payment/event date, while
            // valuation needs the dividend attributable to the financial year.
            // For Indonesian issuers, final dividends are commonly paid in the
            // first half of the following year and interim dividends in the
            // second half of the same year. Use that convention consistently
            // until a source exposes the issuer's fiscal-year attribution.
            var fiscalYear = date.Month <= 6 ? date.Year - 1 : date.Year;

            records.Add(new DividendRecord(
                ticker.ToUpperInvariant(),
                fiscalYear,
                amount,
                date,
                null));
        }

        return records
            .GroupBy(x => x.FiscalYear)
            .Select(g => new DividendRecord(
                g.First().Ticker,
                g.Key,
                g.Sum(x => x.Dps),
                g.Max(x => x.PaymentDate),
                null))
            .OrderBy(x => x.FiscalYear)
            .ToArray();
    }

    private static HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(
            new ProductInfoHeaderValue("Mozilla", "5.0"));
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Referrer = new Uri("https://finance.yahoo.com/");
        return request;
    }

    private sealed class FundamentalValues
    {
        public decimal Revenue { get; private set; }
        public decimal NetIncome { get; private set; }
        public decimal Eps { get; private set; }
        public decimal OperatingCashFlow { get; private set; }
        public decimal CapitalExpenditure { get; private set; }
        public decimal FreeCashFlow { get; private set; }
        public decimal Equity { get; private set; }
        public decimal Debt { get; private set; }
        public decimal Cash { get; private set; }
        public decimal Shares { get; private set; }

        public void Set(string seriesName, decimal value)
        {
            switch (seriesName)
            {
                case "annualTotalRevenue":
                    Revenue = value;
                    break;
                case "annualNetIncome":
                    NetIncome = value;
                    break;
                case "annualDilutedEPS":
                    Eps = value;
                    break;
                case "annualOperatingCashFlow":
                    OperatingCashFlow = value;
                    break;
                case "annualCapitalExpenditure":
                    CapitalExpenditure = value;
                    break;
                case "annualFreeCashFlow":
                    FreeCashFlow = value;
                    break;
                case "annualCommonStockEquity":
                    Equity = value;
                    break;
                case "annualTotalDebt":
                    Debt = value;
                    break;
                case "annualCashCashEquivalentsAndShortTermInvestments":
                    Cash = value;
                    break;
                case "annualDilutedAverageShares":
                case "annualBasicAverageShares":
                    if (Shares == 0)
                        Shares = value;
                    break;
            }
        }
    }

    private static decimal? TryGetDecimal(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsed))
            return parsed;

        return null;
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
}
