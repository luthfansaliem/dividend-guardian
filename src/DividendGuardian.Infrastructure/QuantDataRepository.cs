using DividendGuardian.Quant;
using Npgsql;

namespace DividendGuardian.Infrastructure;

public sealed class QuantDataRepository(Database database)
{
    public async Task<QuantAnalysisInput?> LoadAsync(
        string ticker,
        DateOnly analysisDate,
        CancellationToken ct = default)
    {
        const string stockSql = """
            select ticker, sector, subsector
            from stocks
            where ticker=@ticker and is_active=true;
            """;

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);

        string? sector = null;
        string? subsector = null;
        await using (var command = new NpgsqlCommand(stockSql, connection))
        {
            command.Parameters.AddWithValue("ticker", ticker);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            sector = reader.IsDBNull(1) ? null : reader.GetString(1);
            subsector = reader.IsDBNull(2) ? null : reader.GetString(2);
        }

        var currentPrice = await LoadCurrentPriceAsync(connection, ticker, analysisDate, ct);
        if (currentPrice is null) return null;

        var fundamentals = await LoadFundamentalsAsync(connection, ticker, ct);
        var dividends = await LoadDividendsAsync(connection, ticker, ct);
        var yearEndPrices = await LoadYearEndPricesAsync(connection, ticker, analysisDate, ct);

        if (fundamentals.Count == 0 || dividends.Count == 0 || yearEndPrices.Count == 0)
            return null;

        var isCyclical = IsCyclicalSector(sector);

        return new QuantAnalysisInput(
            ticker,
            currentPrice.Value,
            fundamentals,
            dividends,
            yearEndPrices,
            isCyclical);
    }

    private static async Task<decimal?> LoadCurrentPriceAsync(
        NpgsqlConnection connection, string ticker, DateOnly analysisDate, CancellationToken ct)
    {
        const string sql = """
            select close_price
            from daily_prices
            where ticker=@ticker and trade_date <= @analysis_date and close_price is not null and close_price > 0
            order by trade_date desc
            limit 1;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ticker", ticker);
        command.Parameters.AddWithValue("analysis_date", analysisDate);
        var value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? null : Convert.ToDecimal(value);
    }

    private static async Task<IReadOnlyList<AnnualFundamentalPoint>> LoadFundamentalsAsync(
        NpgsqlConnection connection, string ticker, CancellationToken ct)
    {
        const string sql = """
            select extract(year from period_end)::int, eps, free_cash_flow, net_income, shares_outstanding, total_debt
            from fundamentals
            where ticker=@ticker
              and eps is not null
              and free_cash_flow is not null
              and net_income is not null
              and shares_outstanding is not null
              and shares_outstanding > 0
            order by period_end;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ticker", ticker);
        var result = new List<AnnualFundamentalPoint>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new AnnualFundamentalPoint(
                reader.GetInt32(0),
                reader.GetDecimal(1),
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                reader.GetInt64(4),
                reader.IsDBNull(5) ? 0m : reader.GetDecimal(5)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<AnnualDividendPoint>> LoadDividendsAsync(
        NpgsqlConnection connection, string ticker, CancellationToken ct)
    {
        const string sql = """
            select fiscal_year, dps
            from dividends
            where ticker=@ticker and dps > 0
            order by fiscal_year;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ticker", ticker);
        var result = new List<AnnualDividendPoint>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new AnnualDividendPoint(reader.GetInt32(0), reader.GetDecimal(1)));
        return result;
    }

    private static async Task<IReadOnlyList<AnnualPricePoint>> LoadYearEndPricesAsync(
        NpgsqlConnection connection, string ticker, DateOnly analysisDate, CancellationToken ct)
    {
        const string sql = """
            select distinct on (extract(year from trade_date))
                   extract(year from trade_date)::int, close_price
            from daily_prices
            where ticker=@ticker
              and trade_date <= @analysis_date
              and close_price is not null and close_price > 0
            order by extract(year from trade_date), trade_date desc;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ticker", ticker);
        command.Parameters.AddWithValue("analysis_date", analysisDate);
        var result = new List<AnnualPricePoint>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new AnnualPricePoint(reader.GetInt32(0), reader.GetDecimal(1)));
        return result;
    }

    private static bool IsCyclicalSector(string? sector) =>
        sector is not null &&
        (sector.Equals("Energy", StringComparison.OrdinalIgnoreCase) ||
         sector.Equals("Industrials", StringComparison.OrdinalIgnoreCase) ||
         sector.Equals("Materials", StringComparison.OrdinalIgnoreCase));
}
