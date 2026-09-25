using DividendGuardian.AI;
using DividendGuardian.Domain;
using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class AiAnalysisOrchestratorTests
{
    [Fact]
    public async Task AnalyzeAsync_UsesAnalystWhenSuccessful()
    {
        var expected = CreateResponse("WATCH");
        var orchestrator = new AiAnalysisOrchestrator(new StubAnalyst(expected));

        var result = await orchestrator.AnalyzeAsync(CreateRequest());

        Assert.False(result.UsedFallback);
        Assert.Null(result.FailureReason);
        Assert.Equal("WATCH", result.Response.Verdict);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesDeterministicFallbackWhenAnalystFails()
    {
        var orchestrator = new AiAnalysisOrchestrator(new ThrowingAnalyst());

        var result = await orchestrator.AnalyzeAsync(CreateRequest());

        Assert.True(result.UsedFallback);
        Assert.Contains("API unavailable", result.FailureReason);
        Assert.Equal("deterministic-fallback", result.Response.Model);
        Assert.Equal("PARTIAL", result.Response.DataQuality);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotSwallowCancellation()
    {
        var orchestrator = new AiAnalysisOrchestrator(new CancelingAnalyst());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => orchestrator.AnalyzeAsync(CreateRequest(), cts.Token));
    }

    private static AiAnalysisRequest CreateRequest()
    {
        var score = new QuantScore(
            "ASII", 10m, 20m, 15m, 20m, 10m, 75m, AnalysisStatus.Watch);
        var zone = new BuyZone(
            100m, 110m, 120m, 0.10m, "ACCUMULATE", Array.Empty<string>());
        var quant = new QuantAnalysisResult(
            score,
            new DividendQualityMetrics(null, null, null, null, null, 0, 0),
            90m, 3m, 2m, 10m, 11m, 12m, 10m, 10m,
            new FairValueRange(100m, 110m, 120m),
            0.10m, zone, Array.Empty<string>(), "PARTIAL");

        return new AiAnalysisRequest(
            "ASII", new DateOnly(2026, 9, 25), quant, Array.Empty<AiTriggerEvent>());
    }

    private static AiAnalysisResponse CreateResponse(string verdict) =>
        new(
            "ASII", verdict, "Quant supports review.", "Risks remain.",
            new[] { "Test risk" }, Array.Empty<string>(),
            new[] { "Test invalidation" }, "PARTIAL", "test-model", "DG-AI-TEST");

    private sealed class StubAnalyst(AiAnalysisResponse response) : IAiAnalyst
    {
        public Task<AiAnalysisResponse> AnalyzeAsync(
            AiAnalysisRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private sealed class ThrowingAnalyst : IAiAnalyst
    {
        public Task<AiAnalysisResponse> AnalyzeAsync(
            AiAnalysisRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<AiAnalysisResponse>(
                new InvalidOperationException("API unavailable"));
    }

    private sealed class CancelingAnalyst : IAiAnalyst
    {
        public Task<AiAnalysisResponse> AnalyzeAsync(
            AiAnalysisRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromCanceled<AiAnalysisResponse>(cancellationToken);
    }
}
