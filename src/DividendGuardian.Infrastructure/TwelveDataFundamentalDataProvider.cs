using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DividendGuardian.Infrastructure;

public sealed class TwelveDataFundamentalDataProvider(HttpClient httpClient, IOptions<FundamentalDataOptions> options) : IFundamentalDataProvider
{
    private readonly FundamentalDataOptions _options = options.Value;

    public Task<IReadOnlyCollection<FundamentalRecord>> GetAnnualFundamentalsAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct = default)
        => GetAnnualFundamentalsCoreAsync(ticker, from, to, ct);

    public async Task<IReadOnlyCollection<DividendRecord>> GetDividendsAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var url = BuildUrl("/dividends", ticker, from, to);
        using var response = await httpClient.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, body, ticker, "dividends");

        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("dividends", out var items) || items.ValueKind != JsonValueKind.Array) return [];

        var result = new List<DividendRecord>();
        foreach (var item in items.EnumerateArray())
        {
            var date = ParseDate(item, "payment_date") ?? ParseDate(item, "ex_date") ?? ParseDate(item, "record_date");
            var dps = GetDecimal(item, "amount") ?? GetDecimal(item, "dividend") ?? GetDecimal(item, "dividend_per_share");
            if (dps is null || dps <= 0) continue;
            result.Add(new DividendRecord(ticker, (date ?? from).Year, dps.Value, date, null));
        }

        return result.GroupBy(x => x.FiscalYear).Select(g => g.OrderByDescending(x => x.PaymentDate).First()).OrderBy(x => x.FiscalYear).ToArray();
    }

    private async Task<IReadOnlyCollection<FundamentalRecord>> GetAnnualFundamentalsCoreAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var incomeUrl = BuildUrl("/income_statement", ticker, from, to, "annual");
        using var incomeResponse = await httpClient.GetAsync(incomeUrl, ct);
        var incomeBody = await incomeResponse.Content.ReadAsStringAsync(ct);
        EnsureSuccess(incomeResponse, incomeBody, ticker, "income_statement");

        await DelayAsync(ct);
        JsonDocument? incomeDoc = null, balanceDoc = null, cashFlowDoc = null;
        try
        {
            incomeDoc = JsonDocument.Parse(incomeBody);

            if (_options.IncludeBalanceSheet)
            {
                var url = BuildUrl("/balance_sheet", ticker, from, to, "annual");
                using var response = await httpClient.GetAsync(url, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                EnsureSuccess(response, body, ticker, "balance_sheet");
                balanceDoc = JsonDocument.Parse(body);
                await DelayAsync(ct);
            }

            if (_options.IncludeCashFlow)
            {
                var url = BuildUrl("/cash_flow", ticker, from, to, "annual");
                using var response = await httpClient.GetAsync(url, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                EnsureSuccess(response, body, ticker, "cash_flow");
                cashFlowDoc = JsonDocument.Parse(body);
            }

            var balanceByDate = ReadArray(balanceDoc, "balance_sheet").Where(x => ParseDate(x, "fiscal_date") is not null)
                .ToDictionary(x => ParseDate(x, "fiscal_date")!.Value, x => x);
            var cashByDate = ReadArray(cashFlowDoc, "cash_flow").Where(x => ParseDate(x, "fiscal_date") is not null)
                .ToDictionary(x => ParseDate(x, "fiscal_date")!.Value, x => x);

            var result = new List<FundamentalRecord>();
            foreach (var item in ReadArray(incomeDoc, "income_statement"))
            {
                var periodEnd = ParseDate(item, "fiscal_date");
                if (periodEnd is null || periodEnd < from || periodEnd > to) continue;

                var revenue = GetDecimal(item, "sales") ?? GetNestedDecimal(item, "revenue", "total_revenue") ?? 0m;
                var netIncome = GetDecimal(item, "net_income") ?? GetNestedDecimal(item, "net_income", "net_income_value") ?? 0m;
                var eps = GetDecimal(item, "eps_diluted") ?? GetDecimal(item, "eps_basic") ??
                          GetNestedDecimal(item, "earnings_per_share", "diluted_eps") ??
                          GetNestedDecimal(item, "earnings_per_share", "basic_eps") ?? 0m;
                var shares = (long)(GetDecimal(item, "diluted_shares_outstanding") ?? GetDecimal(item, "basic_shares_outstanding") ?? 0m);

                decimal equity = 0m, debt = 0m, cash = 0m, fcf = 0m;
                if (balanceByDate.TryGetValue(periodEnd.Value, out var balance))
                {
                    equity = GetDecimal(balance, "total_shareholders_equity") ?? GetNestedDecimal(balance, "shareholders_equity", "total_shareholders_equity") ?? 0m;
                    debt = GetDecimal(balance, "total_debt") ?? GetNestedDecimal(balance, "liabilities", "total_debt") ?? 0m;
                    cash = GetDecimal(balance, "cash_and_cash_equivalents") ??
                           GetNestedDecimal(balance, "assets", "cash_and_cash_equivalents") ??
                           GetNestedDecimal(balance, "assets", "cash_cash_equivalents_and_short_term_investments") ?? 0m;
                }
                if (cashByDate.TryGetValue(periodEnd.Value, out var cashFlow))
                    fcf = GetDecimal(cashFlow, "free_cash_flow") ?? GetNestedDecimal(cashFlow, "cash_flow_from_operating_activities", "free_cash_flow") ?? 0m;

                result.Add(new FundamentalRecord(ticker, periodEnd.Value, revenue, netIncome, eps, fcf, equity, debt, cash, shares));
            }
            return result.OrderBy(x => x.PeriodEnd).ToArray();
        }
        finally
        {
            incomeDoc?.Dispose(); balanceDoc?.Dispose(); cashFlowDoc?.Dispose();
        }
    }

    private string BuildUrl(string endpoint, string ticker, DateOnly from, DateOnly to, string? period = null)
    {
        var query = $"symbol={Uri.EscapeDataString(ticker)}&mic_code={Uri.EscapeDataString(_options.MicCode)}" +
                    $"&start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}&outputsize={_options.OutputSize}" +
                    (period is null ? "" : $"&period={period}") +
                    $"&apikey={Uri.EscapeDataString(_options.ApiKey)}";
        return $"{_options.BaseUrl.TrimEnd('/')}{endpoint}?{query}";
    }

    private async Task DelayAsync(CancellationToken ct)
    {
        if (_options.RequestDelayMs > 0) await Task.Delay(_options.RequestDelayMs, ct);
    }

    private static IEnumerable<JsonElement> ReadArray(JsonDocument? document, string property) =>
        document is not null && document.RootElement.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray() : [];

    private static DateOnly? ParseDate(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }

    private static decimal? GetDecimal(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) ? ToDecimal(value) : null;

    private static decimal? GetNestedDecimal(JsonElement item, string parent, string property) =>
        item.TryGetProperty(parent, out var nested) && nested.ValueKind == JsonValueKind.Object ? GetDecimal(nested, property) : null;

    private static decimal? ToDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return null;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string ticker, string endpoint)
    {
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Twelve Data {endpoint} failed for {ticker}: HTTP {(int)response.StatusCode}.");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
            !string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            var message = doc.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() : "provider error";
            throw new InvalidOperationException($"Twelve Data {endpoint} failed for {ticker}: {message}");
        }
    }
}