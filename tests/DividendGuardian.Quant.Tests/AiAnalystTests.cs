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
        var handler = new StubHandler("""
        {
          "output_text": "{"ticker":"ASII","verdict":"WATCH","why_accumulate":"Yield is above its historical baseline.","why_not_accumulate":"Data quality is partial.","key_risks":["Cyclicality"],"data_gaps":["Limited historical FCF"],"invalidation_triggers":["FCF payout deteriorates materially"],"data_quality":"PARTIAL","model":"gpt-test","prompt_version":"DG-AI-1.0"}"
        }
        """);

        var analyst = new AiAnalyst(
            new HttpClient(handler),
            new AiOptions { ApiKey = "test-key", Model = "gpt-test" });

        var result = await analyst.AnalyzeAsync(CreateRequest());

        Assert.Equal("ASII", result.Ticker);
        Assert.Equal("WATCH", result.Verdict);
        Assert.Equal("PARTIAL", result.DataQuality);
        Assert.Single(result.KeyRisks);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsMismatchedTicker()
    {
        var handler = new StubHandler("""
        {
          "output_text": "{"ticker":"TLKM","verdict":"WATCH","why_accumulate":"x","why_not_accumulate":"y","key_risks":[],"data_gaps":[],"invalidation_triggers":[],"data_quality":"READY","model":"gpt-test","prompt_version":"DG-AI-1.0"}"
        }
        """);

        var analyst = new AiAnalyst(
            new HttpClient(handler),
            new AiOptions { ApiKey = "test-key", Model = "gpt-test" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyst.AnalyzeAsync(CreateRequest()));
    }

    [Fact]
    public async Task AnalyzeAsync_ThrowsWhenApiFails()
    {
        var handler = new StubHandler("{"error":"bad request"}", HttpStatusCode.BadRequest);

        var analyst = new AiAnalyst(
            new HttpClient(handler),
            new AiOptions { ApiKey = "test-key", Model = "gpt-test" });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => analyst.AnalyzeAsync(CreateRequest()));
    }

    private static AiAnalysisRequest CreateRequest()
    {
        var score = new QuantScore(
            "ASII", 10m, 20m, 15m, 20m, 10m, 75m,
            AnalysisStatus.Watch);

        var zone = new BuyZone(
            100m, 110m, 120m, 0.10m,
            "ACCUMULATE", Array.Empty<string>());

        var quant = new QuantAnalysisResult(
            score,
            new DividendQualityMetrics(null, null, null, null, null, 0, 0),
            90m, 3m, 2m, 10m, 11m, 12m, 10m,
            10m,
            new FairValueRange(100m, 110m, 120m),
            0.10m,
            zone,
            Array.Empty<string>(),
            "PARTIAL");

        return new AiAnalysisRequest(
            "ASII",
            new DateOnly(2026, 9, 25),
            quant,
            Array.Empty<AiTriggerEvent>());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _statusCode;

        public StubHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _body = body;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.False(string.IsNullOrWhiteSpace(request.Headers.Authorization?.Parameter));
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri?.ToString());

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(
                    _body,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
