using System.Globalization;
using System.Text;

namespace DividendGuardian.Infrastructure;

public sealed class CsvFundamentalDataProvider : IFundamentalDataProvider
{
    private readonly FundamentalDataOptions _options;

    public CsvFundamentalDataProvider(Microsoft.Extensions.Options.IOptions<FundamentalDataOptions> options)
        => _options = options.Value;

    public async Task<IReadOnlyCollection<FundamentalRecord>> GetAnnualFundamentalsAsync(
        string ticker, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var path = ResolvePath(_options.CsvDirectory, _options.FundamentalsFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Fundamental CSV file was not found.", path);

        var rows = await ReadRowsAsync(path, ct);
        var result = new List<FundamentalRecord>();

        foreach (var row in rows)
        {
            if (!EqualsTicker(row, ticker)) continue;
            var periodEnd = ParseDate(row, "period_end");
            if (periodEnd < from || periodEnd > to) continue;

            result.Add(new FundamentalRecord(
                ticker,
                periodEnd,
                RequiredDecimal(row, "revenue"),
                RequiredDecimal(row, "net_income"),
                RequiredDecimal(row, "eps"),
                RequiredDecimal(row, "free_cash_flow"),
                RequiredDecimal(row, "equity"),
                RequiredDecimal(row, "debt"),
                RequiredDecimal(row, "cash"),
                RequiredLong(row, "shares_outstanding")));
        }

        return result
            .GroupBy(x => x.PeriodEnd)
            .Select(g => g.Last())
            .OrderBy(x => x.PeriodEnd)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<DividendRecord>> GetDividendsAsync(
        string ticker, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var path = ResolvePath(_options.CsvDirectory, _options.DividendsFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Dividend CSV file was not found.", path);

        var rows = await ReadRowsAsync(path, ct);
        var result = new List<DividendRecord>();

        foreach (var row in rows)
        {
            if (!EqualsTicker(row, ticker)) continue;

            var year = RequiredInt(row, "fiscal_year");
            if (year < from.Year || year > to.Year) continue;

            var paymentDate = OptionalDate(row, "payment_date");
            result.Add(new DividendRecord(
                ticker,
                year,
                RequiredDecimal(row, "dps"),
                paymentDate,
                OptionalDecimal(row, "payout_ratio")));
        }

        return result
            .GroupBy(x => x.FiscalYear)
            .Select(g => g.Last())
            .OrderBy(x => x.FiscalYear)
            .ToArray();
    }

    private static string ResolvePath(string directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("FUNDAMENTALS_CSV_DIRECTORY is required when using the CSV provider.");

        return Path.IsPathRooted(directory)
            ? Path.Combine(directory, fileName)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, directory, fileName));
    }

    private static async Task<IReadOnlyList<Dictionary<string, string>>> ReadRowsAsync(string path, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8, ct);
        if (lines.Length == 0) return [];

        var headers = ParseCsvLine(lines[0]).Select(x => x.Trim().ToLowerInvariant()).ToArray();
        if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace))
            throw new FormatException($"CSV header is invalid: {path}");

        var rows = new List<Dictionary<string, string>>();
        for (var i = 1; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var values = ParseCsvLine(lines[i]);
            if (values.Count != headers.Length)
                throw new FormatException($"CSV row {i + 1} has {values.Count} columns; expected {headers.Length}: {path}");

            rows.Add(headers.Select((h, index) => new { h, value = values[index].Trim() })
                .ToDictionary(x => x.h, x => x.value));
        }

        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else value.Append(c);
        }

        if (quoted)
            throw new FormatException("CSV contains an unterminated quoted field.");

        values.Add(value.ToString());
        return values;
    }

    private static bool EqualsTicker(IReadOnlyDictionary<string, string> row, string ticker) =>
        row.TryGetValue("ticker", out var value) &&
        string.Equals(value.Trim(), ticker.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Required(IReadOnlyDictionary<string, string> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            throw new FormatException($"CSV field '{name}' is required.");
        return value;
    }

    private static decimal RequiredDecimal(IReadOnlyDictionary<string, string> row, string name) =>
        decimal.TryParse(Required(row, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"CSV field '{name}' is not a valid decimal.");

    private static decimal? OptionalDecimal(IReadOnlyDictionary<string, string> row, string name) =>
        row.TryGetValue(name, out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException($"CSV field '{name}' is not a valid decimal.")
            : null;

    private static long RequiredLong(IReadOnlyDictionary<string, string> row, string name) =>
        long.TryParse(Required(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"CSV field '{name}' is not a valid integer.");

    private static int RequiredInt(IReadOnlyDictionary<string, string> row, string name) =>
        int.TryParse(Required(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"CSV field '{name}' is not a valid integer.");

    private static DateOnly ParseDate(IReadOnlyDictionary<string, string> row, string name) =>
        DateOnly.TryParseExact(Required(row, name), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException($"CSV field '{name}' must use yyyy-MM-dd.");

    private static DateOnly? OptionalDate(IReadOnlyDictionary<string, string> row, string name)
    {
        if (!row.TryGetValue(name, out var raw) || string.IsNullOrWhiteSpace(raw)) return null;
        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException($"CSV field '{name}' must use yyyy-MM-dd.");
    }
}
