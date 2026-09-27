using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DividendGuardian.Infrastructure;

public sealed class YahooFinanceFundamentalDataProvider(
    HttpClient httpClient) : IFundamentalDataProvider
{
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

        var symbol = Uri.EscapeDataString(ticker.ToUpperInvariant());
        var url =
            $"https://query1.finance.yahoo.com/v10/finance/quoteSummary/{symbol}" +
            "?modules=incomeStatementHistory,balanceSheetHistory,cashflowStatementHistory,defaultKeyStatistics";

        using var request = CreateRequest(url);
        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Yahoo Finance fundamentals HTTP {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);
        var result = json.RootElement
            .GetProperty("quoteSummary")
            .GetProperty("result");

        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
            return [];

        var root = result[0];
        var income = root.GetProperty("incomeStatementHistory").GetProperty("incomeStatementHistory");
        var balance = root.GetProperty("balanceSheetHistory").GetProperty("balanceSheetStatements");
        var cashFlow = root.GetProperty("cashflowStatementHistory").GetProperty("cashflowStatements");

        var balanceByDate = balance
            .EnumerateArray()
            .Select(x => new { Item = x, Date = GetDate(x, "endDate") })
            .Where(x => x.Date is not null)
            .ToDictionary(x => x.Date!.Value, x => x.Item);

        var cashFlowByDate = cashFlow
            .EnumerateArray()
            .Select(x => new { Item = x, Date = GetDate(x, "endDate") })
            .Where(x => x.Date is not null)
            .ToDictionary(x => x.Date!.Value, x => x.Item);

        var resultRows = new List<FundamentalRecord>();

        foreach (var item in income.EnumerateArray())
        {
            var periodEnd = GetDate(item, "endDate");
            if (periodEnd is null || periodEnd < from || periodEnd > to)
                continue;

            var revenue = GetDecimal(item, "totalRevenue") ?? 0m;
            var netIncome = GetDecimal(item, "netIncome") ?? 0m;
            var eps = GetDecimal(item, "dilutedEPS") ?? GetDecimal(item, "basicEPS") ?? 0m;

            decimal equity = 0m;
            decimal debt = 0m;
            decimal cash = 0m;
            decimal fcf = 0m;
            long shares = 0;

            if (balanceByDate.TryGetValue(periodEnd.Value, out var balanceItem))
            {
                equity = GetDecimal(balanceItem, "totalStockholderEquity") ?? 0m;
                debt = GetDecimal(balanceItem, "totalDebt") ??
                       GetDecimal(balanceItem, "longTermDebt") ?? 0m;
                cash = GetDecimal(balanceItem, "cash") ??
                       GetDecimal(balanceItem, "cashCashEquivalentsAndShortTermInvestments") ?? 0m;
            }

            if (cashFlowByDate.TryGetValue(periodEnd.Value, out var cashFlowItem))
            {
                var operatingCashFlow = GetDecimal(cashFlowItem, "totalCashFromOperatingActivities") ?? 0m;
                var capitalExpenditure = GetDecimal(cashFlowItem, "capitalExpenditures") ?? 0m;
                fcf = operatingCashFlow + capitalExpenditure;
            }

            if (shares == 0)
                shares = (long)(GetDecimal(item, "weightedAverageSharesDiluted") ??
                                GetDecimal(item, "weightedAverageSharesBasic") ?? 0m);

            resultRows.Add(new FundamentalRecord(
                ticker.ToUpperInvariant(),
                periodEnd.Value,
                revenue,
                netIncome,
                eps,
                fcf,
                equity,
                debt,
                cash,
                shares));
        }

        return resultRows
            .GroupBy(x => x.PeriodEnd)
            .Select(x => x.First())
            .OrderBy(x => x.PeriodEnd)
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

            if (!long.TryParse(dividend.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
                continue;

            var date = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.Date);

            if (date < from || date > to)
                continue;

            records.Add(new DividendRecord(
                ticker.ToUpperInvariant(),
                date.Year,
                amount,
                date,
                null));
        }

        return records
            .GroupBy(x => x.FiscalYear)
            .Select(g => new DividendRecord(
                g.Key == 0 ? ticker.ToUpperInvariant() : g.First().Ticker,
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
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static DateOnly? GetDate(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("raw", out var raw) &&
            raw.ValueKind == JsonValueKind.Number &&
            raw.TryGetInt64(out var timestamp))
        {
            return DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.Date);
        }

        if (value.ValueKind == JsonValueKind.String &&
            DateOnly.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return date;

        return null;
    }

    private static decimal? GetDecimal(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("raw", out var raw))
            return TryGetDecimal(raw, out var rawValue) ? rawValue : null;

        return TryGetDecimal(value, out var valueResult) ? valueResult : null;
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
