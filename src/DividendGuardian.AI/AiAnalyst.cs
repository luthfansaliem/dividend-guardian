using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DividendGuardian.AI;

public sealed class AiAnalyst : IAiAnalyst
{
    private const string PromptVersion = "DG-AI-1.0";
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;

    public AiAnalyst(HttpClient httpClient, AiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<AiAnalysisResponse> AnalyzeAsync(AiAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("OPENAI_API_KEY is not configured.");

        var analystInput = new
        {
            ticker = request.Ticker,
            analysis_date = request.AnalysisDate.ToString("yyyy-MM-dd"),
            triggers = request.Triggers.Select(x => new { type = x.Trigger.ToString(), occurred_at = x.OccurredAt, description = x.Description }),
            quant = request.Quant
        };

        var body = new
        {
            model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-5.6-luna" : _options.Model,
            input = new object[]
            {
                new { role = "system", content = new[] { new { type = "input_text", text = DividendGuardianAiPrompt.System } } },
                new { role = "user", content = new[] { new { type = "input_text", text = JsonSerializer.Serialize(analystInput, JsonOptions) } } }
            },
            text = new { format = new { type = "json_schema", name = "dividend_guardian_analysis", strict = true, schema = ResponseSchema } },
            store = false
        };

        var responseBody = await SendWithRetryAsync(body, cancellationToken);
        var outputText = ExtractOutputText(responseBody);
        var result = JsonSerializer.Deserialize<AiAnalysisResponse>(outputText, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI returned an empty AI analysis.");

        ValidateResponse(result, request.Ticker);

        return result with
        {
            Model = string.IsNullOrWhiteSpace(result.Model) ? _options.Model : result.Model,
            PromptVersion = string.IsNullOrWhiteSpace(result.PromptVersion) ? PromptVersion : result.PromptVersion
        };
    }

    private async Task<string> SendWithRetryAsync(object body, CancellationToken cancellationToken)
    {
        var maxRetries = Math.Max(0, _options.MaxRetries);

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                httpRequest.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                    return responseBody;

                if (!IsTransient(response.StatusCode) || attempt >= maxRetries)
                    throw new HttpRequestException(
                        $"OpenAI Responses API returned {(int)response.StatusCode}: {responseBody}",
                        null, response.StatusCode);

                await DelayBeforeRetryAsync(attempt, response, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxRetries)
            {
                await DelayBeforeRetryAsync(attempt, null, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null && attempt < maxRetries)
            {
                await DelayBeforeRetryAsync(attempt, null, cancellationToken);
            }
        }
    }

    private async Task DelayBeforeRetryAsync(
        int attempt,
        HttpResponseMessage? response,
        CancellationToken cancellationToken)
    {
        var retryAfter = response is not null && response.StatusCode == HttpStatusCode.TooManyRequests
            ? GetRetryAfter(response)
            : null;

        var baseDelay = Math.Max(0, _options.RetryDelayMs);
        var exponentialMs = Math.Min(baseDelay * Math.Pow(2, attempt), 10_000);
        var delay = retryAfter ?? TimeSpan.FromMilliseconds(exponentialMs);

        if (delay > TimeSpan.FromSeconds(10))
            delay = TimeSpan.FromSeconds(10);

        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta >= TimeSpan.Zero)
            return delta;

        if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var value = values.FirstOrDefault();
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) &&
                seconds >= 0)
                return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static string ExtractOutputText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);

        if (document.RootElement.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(outputText.GetString()))
            return outputText.GetString()!;

        if (!document.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("OpenAI response did not contain output text.");

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(text.GetString()))
                    return text.GetString()!;
            }
        }

        throw new InvalidOperationException("OpenAI response contained no text content.");
    }

    private static void ValidateResponse(AiAnalysisResponse result, string expectedTicker)
    {
        if (!string.Equals(result.Ticker, expectedTicker, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI response ticker does not match the request.");
        if (string.IsNullOrWhiteSpace(result.Verdict) ||
            string.IsNullOrWhiteSpace(result.WhyAccumulate) ||
            string.IsNullOrWhiteSpace(result.WhyNotAccumulate) ||
            string.IsNullOrWhiteSpace(result.DataQuality))
            throw new InvalidOperationException("AI response is missing required analysis fields.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static readonly object ResponseSchema = new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            ticker = new { type = "string" },
            verdict = new { type = "string" },
            why_accumulate = new { type = "string" },
            why_not_accumulate = new { type = "string" },
            key_risks = new { type = "array", items = new { type = "string" } },
            data_gaps = new { type = "array", items = new { type = "string" } },
            invalidation_triggers = new { type = "array", items = new { type = "string" } },
            data_quality = new { type = "string" },
            model = new { type = "string" },
            prompt_version = new { type = "string" }
        },
        required = new[] { "ticker", "verdict", "why_accumulate", "why_not_accumulate", "key_risks", "data_gaps", "invalidation_triggers", "data_quality", "model", "prompt_version" }
    };
}
