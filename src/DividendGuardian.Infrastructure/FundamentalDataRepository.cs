using Npgsql;

namespace DividendGuardian.Infrastructure;

public sealed class FundamentalDataRepository(Database database)
{
    public async Task UpsertFundamentalsAsync(IReadOnlyCollection<FundamentalRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        const string sql = """
            insert into fundamentals (ticker, period_end, revenue, net_income, eps, free_cash_flow, equity, total_debt, cash, shares_outstanding)
            values (@ticker, @period_end, @revenue, @net_income, @eps, @fcf, @equity, @debt, @cash, @shares)
            on conflict (ticker, period_end) do update set
                revenue=excluded.revenue, net_income=excluded.net_income, eps=excluded.eps,
                free_cash_flow=excluded.free_cash_flow, equity=excluded.equity, total_debt=excluded.total_debt,
                cash=excluded.cash, shares_outstanding=excluded.shares_outstanding;
            """;
        foreach (var record in records)
        {
            await using var command = new NpgsqlCommand(sql, connection, tx);
            command.Parameters.AddWithValue("ticker", record.Ticker);
            command.Parameters.AddWithValue("period_end", record.PeriodEnd);
            command.Parameters.AddWithValue("revenue", record.Revenue);
            command.Parameters.AddWithValue("net_income", record.NetIncome);
            command.Parameters.AddWithValue("eps", record.Eps);
            command.Parameters.AddWithValue("fcf", record.FreeCashFlow);
            command.Parameters.AddWithValue("equity", record.Equity);
            command.Parameters.AddWithValue("debt", record.Debt);
            command.Parameters.AddWithValue("cash", record.Cash);
            command.Parameters.AddWithValue("shares", record.SharesOutstanding);
            await command.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task UpsertDividendsAsync(IReadOnlyCollection<DividendRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        const string sql = """
            insert into dividends (ticker, fiscal_year, dps, payment_date, payout_ratio)
            values (@ticker, @year, @dps, @payment_date, @payout_ratio)
            on conflict (ticker, fiscal_year) do update set
                dps=excluded.dps, payment_date=excluded.payment_date, payout_ratio=excluded.payout_ratio;
            """;
        foreach (var record in records)
        {
            await using var command = new NpgsqlCommand(sql, connection, tx);
            command.Parameters.AddWithValue("ticker", record.Ticker);
            command.Parameters.AddWithValue("year", record.FiscalYear);
            command.Parameters.AddWithValue("dps", record.Dps);
            command.Parameters.AddWithValue("payment_date", (object?)record.PaymentDate ?? DBNull.Value);
            command.Parameters.AddWithValue("payout_ratio", (object?)record.PayoutRatio ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task RecordIngestionAsync(string provider, string ticker, DateOnly from, DateOnly to, int fundamentalRows, int dividendRows, string status, string? error, CancellationToken ct = default)
    {
        const string sql = """
            insert into fundamental_ingestion_runs
                (provider, ticker, from_date, to_date, fundamental_rows, dividend_rows, status, error_message)
            values
                (@provider, @ticker, @from_date, @to_date, @fundamental_rows, @dividend_rows, @status, @error);
            """;
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("ticker", ticker);
        command.Parameters.AddWithValue("from_date", from);
        command.Parameters.AddWithValue("to_date", to);
        command.Parameters.AddWithValue("fundamental_rows", fundamentalRows);
        command.Parameters.AddWithValue("dividend_rows", dividendRows);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("error", (object?)error ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }
}