using System.Text.Json;
using DividendGuardian.AI;
using Npgsql;

namespace DividendGuardian.Infrastructure;

public sealed class AiAnalysisRepository(Database database)
{
    public async Task SaveAsync(
        AiAnalysisResponse response,
        IReadOnlyCollection<AiTriggerEvent> triggers,
        CancellationToken ct = default)
    {
        var triggerType = triggers.Count == 0
            ? AiAnalysisTrigger.ManualReview.ToString()
            : triggers.First().Trigger.ToString();

        const string sql = """
            insert into ai_analysis
                (ticker, analysis_time, trigger_type, model, status, summary,
                 why_accumulate, why_not_accumulate, risks, invalidation_triggers,
                 data_gaps, data_quality, raw_response, model_version)
            values
                (@ticker, now(), @trigger_type, @model, @status, @summary,
                 @why_accumulate, @why_not_accumulate, @risks,
                 @invalidation_triggers, @data_gaps, @data_quality,
                 @raw_response, @model_version);
            """;

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("ticker", response.Ticker);
        command.Parameters.AddWithValue("trigger_type", triggerType);
        command.Parameters.AddWithValue("model", response.Model);
        command.Parameters.AddWithValue("status", "SUCCESS");
        command.Parameters.AddWithValue("summary", response.Verdict);
        command.Parameters.AddWithValue("why_accumulate", response.WhyAccumulate);
        command.Parameters.AddWithValue("why_not_accumulate", response.WhyNotAccumulate);
        command.Parameters.AddWithValue("risks", JsonSerializer.Serialize(response.KeyRisks));
        command.Parameters.AddWithValue("invalidation_triggers", JsonSerializer.Serialize(response.InvalidationTriggers));
        command.Parameters.AddWithValue("data_gaps", JsonSerializer.Serialize(response.DataGaps));
        command.Parameters.AddWithValue("data_quality", response.DataQuality);
        command.Parameters.AddWithValue("raw_response", JsonSerializer.SerializeToDocument(response).RootElement.GetRawText());
        command.Parameters.AddWithValue("model_version", response.PromptVersion);

        await command.ExecuteNonQueryAsync(ct);
    }
}
