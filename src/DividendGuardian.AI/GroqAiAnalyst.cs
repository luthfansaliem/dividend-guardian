using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DividendGuardian.AI;

public sealed class GroqAiAnalyst(HttpClient httpClient, AiOptions options) : IAiAnalyst
{
    private const string PromptVersion = "DG-AI-1.0";

    public async Task<AiAnalysisResponse> AnalyzeAsync(AiAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.GroqApiKey))
            throw new InvalidOperationException("GROQ_API_KEY is not configured.");

        var model = string.IsNullOrWhiteSpace(options.GroqModel) ? "openai/gpt-oss-20b" : options.GroqModel;
        var analystInput = new
        {
            ticker = request.Ticker,
            analysis_date = request.AnalysisDate.ToString("yyyy-MM-dd"),
            triggers = request.Triggers.Select(x => new { type = x.Trigger.ToString(), occurred_at = x.OccurredAt, description = x.Description }),
            quant = request.Quant
        };

        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = DividendGuardianAiPrompt.System + "\nReturn every field required by the supplied JSON schema. Use empty arrays rather than omitting array fields when no items apply. Do not emit markdown or prose outside the schema." },
                new { role = "user", content = JsonSerializer.Serialize(analystInput, JsonOptions) }
            },
            reasoning_effort = "low",
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "dividend_guardian_analysis",
                    strict = true,
                    schema = ResponseSchema.RootElement
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.GroqApiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Groq Chat Completions API returned {(int)response.StatusCode}: {responseBody}", null, response.StatusCode);

        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Groq returned an empty AI analysis.");

        var result = JsonSerializer.Deserialize<AiAnalysisResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("Groq returned an empty AI analysis.");

        if (!string.Equals(result.Ticker, request.Ticker, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI response ticker does not match the request.");
        if (string.IsNullOrWhiteSpace(result.Verdict) ||
            string.IsNullOrWhiteSpace(result.WhyAccumulate) ||
            string.IsNullOrWhiteSpace(result.WhyNotAccumulate) ||
            string.IsNullOrWhiteSpace(result.DataQuality))
            throw new InvalidOperationException("AI response is missing required analysis fields.");

        return result with
        {
            Model = model,
            PromptVersion = string.IsNullOrWhiteSpace(result.PromptVersion) ? PromptVersion : result.PromptVersion
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonDocument ResponseSchema = JsonDocument.Parse("""
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "ticker": { "type": "string" },
        "verdict": { "type": "string" },
        "why_accumulate": { "type": "string" },
        "why_not_accumulate": { "type": "string" },
        "key_risks": { "type": "array", "items": { "type": "string" } },
        "data_gaps": { "type": "array", "items": { "type": "string" } },
        "invalidation_triggers": { "type": "array", "items": { "type": "string" } },
        "data_quality": { "type": "string" },
        "model": { "type": "string" },
        "prompt_version": { "type": "string" }
      },
      "required": [
        "ticker", "verdict", "why_accumulate", "why_not_accumulate",
        "key_risks", "data_gaps", "invalidation_triggers", "data_quality",
        "model", "prompt_version"
      ]
    }
    """);
}
