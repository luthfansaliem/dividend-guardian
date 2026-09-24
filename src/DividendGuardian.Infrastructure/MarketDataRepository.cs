using Npgsql;

namespace DividendGuardian.Infrastructure;

public sealed class MarketDataRepository(Database database)
{
    public async Task UpsertEodPricesAsync(IReadOnlyCollection<EodPrice> prices, CancellationToken ct = default)
    {
        if (prices.Count == 0) return;
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        const string sql = """
            insert into daily_prices (ticker, trade_date, open_price, high_price, low_price, close_price, volume)
            values (@ticker, @trade_date, @open, @high, @low, @close, @volume)
            on conflict (ticker, trade_date) do update set
                open_price=excluded.open_price, high_price=excluded.high_price,
                low_price=excluded.low_price, close_price=excluded.close_price, volume=excluded.volume;
            """;
        foreach (var price in prices)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("ticker", price.Ticker);
            command.Parameters.AddWithValue("trade_date", price.TradeDate);
            command.Parameters.AddWithValue("open", price.Open);
            command.Parameters.AddWithValue("high", price.High);
            command.Parameters.AddWithValue("low", price.Low);
            command.Parameters.AddWithValue("close", price.Close);
            command.Parameters.AddWithValue("volume", price.Volume);
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task RecordIngestionAsync(string provider, string ticker, DateOnly from, DateOnly to, int rows, string status, string? error, CancellationToken ct = default)
    {
        const string sql = """
            insert into market_data_ingestion_runs (provider, ticker, from_date, to_date, rows_written, status, error_message)
            values (@provider, @ticker, @from_date, @to_date, @rows, @status, @error);
            """;
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("ticker", ticker);
        command.Parameters.AddWithValue("from_date", from);
        command.Parameters.AddWithValue("to_date", to);
        command.Parameters.AddWithValue("rows", rows);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("error", (object?)error ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }
}
