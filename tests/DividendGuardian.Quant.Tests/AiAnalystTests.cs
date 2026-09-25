using System.Net;
using System.Text;
using DividendGuardian.AI;
using DividendGuardian.Domain;
using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class AiAnalystTests
{
    [Fact]
    public async Task AnalyzeAsync_ParsesStructuredResponsesOutput()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.OK, """{"output_text":"{\"ticker\":\"ASII\",\"verdict\":\"WATCH\",\"why_accumulate\":\"Yield is above its historical baseline.\",\"why_not_accumulate\":\"Data quality is partial.\",\"key_risks\":[\"Cyclicality\"],\"data_gaps\":[\"Limited historical FCF\"],\"invalidation_triggers\":[\"FCF payout deteriorates materially\"],\"data_quality\":\"PARTIAL\",\"model\":\"gpt-test\",\"prompt_version\":\"DG-AI-1.0\"}"}"""));

        var analyst = CreateAnalyst(handler);
        var result = await analyst.AnalyzeAsync(CreateRequest());

        Assert.Equal("ASII", result.Ticker);
        Assert.Equal("WATCH", result.Verdict);
        Assert.Equal("PARTIAL", result.DataQuality);
        Assert.Single(result.KeyRisks);
    }

    [Fact]
    public async Task AnalyzeAsync_RetriesTransientThenSucceeds()
    {
        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new(HttpStatusCode.ServiceUnavailable, """{"error":"temporary"}""")
                : new(HttpStatusCode.OK, ValidResponse());
        });

        var result = await CreateAnalyst(handler, maxRetries: 1)
            .AnalyzeAsync(CreateRequest());

        Assert.Equal("ASII", result.Ticker);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotRetryBadRequest()
    {
        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            calls++;
            return new(HttpStatusCode.BadRequest, """{"error":"bad request"}""");
        });

        var analyst = CreateAnalyst(handler, maxRetries: 3);

        await Assert.ThrowsAsync<HttpRequestException>(() => analyst.AnalyzeAsync(CreateRequest()));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AnalyzeAsync_RetriesTooManyRequestsUntilExhausted()
    {
        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            calls++;
            return new(HttpStatusCode.TooManyRequests, """{"error":"rate limited"}""");
        });

        var analyst = CreateAnalyst(handler, maxRetries: 2);

        await Assert.ThrowsAsync<HttpRequestException>(() => analyst.AnalyzeAsync(CreateRequest()));
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsMismatchedTicker()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.OK, """{"output_text":"{\"ticker\":\"TLKM\",\"verdict\":\"WATCH\",\"why_accumulate\":\"x\",\"why_not_accumulate\":\"y\",\"key_risks\":[],\"data_gaps\":[],\"invalidation_triggers\":[],\"data_quality\":\"READY\",\"model\":\"gpt-test\",\"prompt_version\":\"DG-AI-1.0\"}"}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAnalyst(handler).AnalyzeAsync(CreateRequest()));
    }

    [Fact]
    public async Task AnalyzeAsync_ThrowsWhenApiFails()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.BadRequest, """{"error":"bad request"}"""));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => CreateAnalyst(handler).AnalyzeAsync(CreateRequest()));
    }

    private static AiAnalyst CreateAnalyst(StubHandler handler, int maxRetries = 0) =>
        new(new HttpClient(handler), new AiOptions
        {
            ApiKey = "test-key",
            Model = "gpt-test",
            MaxRetries = maxRetries,
            RetryDelayMs = 0
        });

    private static string ValidResponse() =>
        """{"output_text":"{\"ticker\":\"ASII\",\"verdict\":\"WATCH\",\"why_accumulate\":\"x\",\"why_not_accumulate\":\"y\",\"key_risks\":[],\"data_gaps\":[],\"invalidation_triggers\":[],\"data_quality\":\"READY\",\"model\":\"gpt-test\",\"prompt_version\":\"DG-AI-1.0\"}"}""";

    private static AiAnalysisRequest CreateRequest()
    {
        var score = new QuantScore("ASII", 10m, 20m, 15m, 20m, 10m, 75m, AnalysisStatus.Watch);
        var zone = new BuyZone(100m, 110m, 120m, 0.10m, "ACCUMULATE", Array.Empty<string>());
        var quant = new QuantAnalysisResult(
            score,
            new DividendQualityMetrics(null, null, null, null, null, 0, 0),
            90m, 3m, 2m, 10m, 11m, 12m, 10m, 10m,
            new FairValueRange(100m, 110m, 120m), 0.10m, zone,
            Array.Empty<string>(), "PARTIAL");

        return new AiAnalysisRequest("ASII", new DateOnly(2026, 9, 25), quant, Array.Empty<AiTriggerEvent>());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.False(string.IsNullOrWhiteSpace(request.Headers.Authorization?.Parameter));
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri?.ToString());
            return Task.FromResult(responder(request));
        }
    }
}
