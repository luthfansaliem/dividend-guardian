using DividendGuardian.Quant;
using Npgsql;

namespace DividendGuardian.Infrastructure;

public sealed class QuantAnalysisRepository(Database database)
{
    public async Task SaveAsync(
        QuantAnalysisResult result,
        DateOnly analysisDate,
        CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string valuationSql = """
            insert into valuations
                (ticker, valuation_date, price, dividend_yield, pe, fcf_yield)
            values
                (@ticker, @date, @price, @yield, @pe, @fcf_yield)
            on conflict (ticker, valuation_date) do update set
                price=excluded.price,
                dividend_yield=excluded.dividend_yield,
                pe=excluded.pe,
                fcf_yield=excluded.fcf_yield;
            """;

        await using (var command = new NpgsqlCommand(valuationSql, connection, transaction))
        {
            command.Parameters.AddWithValue("ticker", result.Score.Ticker);
            command.Parameters.AddWithValue("date", analysisDate);
            command.Parameters.AddWithValue("price", result.CurrentPrice);
            command.Parameters.AddWithValue("yield", result.CurrentDividendYieldPercent);
            command.Parameters.AddWithValue("pe", (object?)result.CurrentPe ?? DBNull.Value);
            command.Parameters.AddWithValue("fcf_yield", (object?)result.CurrentFcfYieldPercent ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(ct);
        }

        const string fairValueSql = """
            insert into fair_values
                (ticker, analysis_date, conservative_value, base_value, optimistic_value,
                 margin_of_safety, model_version)
            values
                (@ticker, @date, @conservative, @base, @optimistic, @margin_of_safety, @model)
            on conflict (ticker, analysis_date) do update set
                conservative_value=excluded.conservative_value,
                base_value=excluded.base_value,
                optimistic_value=excluded.optimistic_value,
                margin_of_safety=excluded.margin_of_safety,
                model_version=excluded.model_version;
            """;

        await using (var command = new NpgsqlCommand(fairValueSql, connection, transaction))
        {
            command.Parameters.AddWithValue("ticker", result.Score.Ticker);
            command.Parameters.AddWithValue("date", analysisDate);
            command.Parameters.AddWithValue("conservative", (object?)result.FairValue.Conservative ?? DBNull.Value);
            command.Parameters.AddWithValue("base", (object?)result.FairValue.Base ?? DBNull.Value);
            command.Parameters.AddWithValue("optimistic", (object?)result.FairValue.Optimistic ?? DBNull.Value);
            command.Parameters.AddWithValue("margin_of_safety", (object?)result.MarginOfSafety ?? DBNull.Value);
            command.Parameters.AddWithValue("model", "DG-1.0");
            await command.ExecuteNonQueryAsync(ct);
        }

        const string scoreSql = """
            insert into quant_scores
                (ticker, analysis_date, dividend_yield_score, sustainability_score,
                 growth_score, valuation_score, risk_score, total_score, status, model_version)
            values
                (@ticker, @date, @yield_score, @sustainability, @growth,
                 @valuation, @risk, @total, @status, @model)
            on conflict (ticker, analysis_date) do update set
                dividend_yield_score=excluded.dividend_yield_score,
                sustainability_score=excluded.sustainability_score,
                growth_score=excluded.growth_score,
                valuation_score=excluded.valuation_score,
                risk_score=excluded.risk_score,
                total_score=excluded.total_score,
                status=excluded.status,
                model_version=excluded.model_version;
            """;

        await using (var command = new NpgsqlCommand(scoreSql, connection, transaction))
        {
            command.Parameters.AddWithValue("ticker", result.Score.Ticker);
            command.Parameters.AddWithValue("date", analysisDate);
            command.Parameters.AddWithValue("yield_score", result.Score.DividendYieldScore);
            command.Parameters.AddWithValue("sustainability", result.Score.SustainabilityScore);
            command.Parameters.AddWithValue("growth", result.Score.GrowthScore);
            command.Parameters.AddWithValue("valuation", result.Score.ValuationScore);
            command.Parameters.AddWithValue("risk", result.Score.RiskScore);
            command.Parameters.AddWithValue("total", result.Score.TotalScore);
            command.Parameters.AddWithValue("status", result.Score.Status.ToString().ToUpperInvariant());
            command.Parameters.AddWithValue("model", "DG-1.0");
            await command.ExecuteNonQueryAsync(ct);
        }

        const string runSql = """
            insert into quant_analysis_runs
                (analysis_date, ticker, status, total_score, analysis_status, data_quality)
            values
                (@date, @ticker, 'SUCCESS', @total, @analysis_status, 'READY');
            """;

        await using (var command = new NpgsqlCommand(runSql, connection, transaction))
        {
            command.Parameters.AddWithValue("date", analysisDate);
            command.Parameters.AddWithValue("ticker", result.Score.Ticker);
            command.Parameters.AddWithValue("total", result.Score.TotalScore);
            command.Parameters.AddWithValue("analysis_status", result.Score.Status.ToString().ToUpperInvariant());
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task RecordFailureAsync(
        string ticker,
        DateOnly analysisDate,
        string error,
        CancellationToken ct = default)
    {
        const string sql = """
            insert into quant_analysis_runs
                (analysis_date, ticker, status, data_quality, error_message)
            values
                (@date, @ticker, 'FAILED', 'ERROR', @error);
            """;
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("date", analysisDate);
        command.Parameters.AddWithValue("ticker", ticker);
        command.Parameters.AddWithValue("error", error);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordInsufficientDataAsync(
        string ticker,
        DateOnly analysisDate,
        CancellationToken ct = default)
    {
        const string sql = """
            insert into quant_analysis_runs
                (analysis_date, ticker, status, data_quality)
            values
                (@date, @ticker, 'SKIPPED', 'INSUFFICIENT');
            """;
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("date", analysisDate);
        command.Parameters.AddWithValue("ticker", ticker);
        await command.ExecuteNonQueryAsync(ct);
    }
}
