using DividendGuardian.AI;
using DividendGuardian.Domain;
using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class AiTriggerPolicyTests
{
    [Fact]
    public void ShouldAnalyze_WhenExternalTriggerExists()
    {
        var policy = new AiTriggerPolicy();
        var current = CreateQuant("WATCH", 100m, 60m);
        var trigger = new AiTriggerEvent(AiAnalysisTrigger.NewFinancialReport, DateTimeOffset.UtcNow);

        Assert.True(policy.ShouldAnalyze(
            current, null, new[] { trigger }, null,
            DateTimeOffset.UtcNow, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ShouldNotAnalyze_WhenInsideCooldown()
    {
        var policy = new AiTriggerPolicy();
        var current = CreateQuant("ACCUMULATE", 100m, 75m);
        var last = new DateTimeOffset(2026, 9, 25, 7, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

        Assert.False(policy.ShouldAnalyze(
            current, null, Array.Empty<AiTriggerEvent>(), last, now,
            TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ShouldAnalyze_WhenCooldownHasExpired()
    {
        var policy = new AiTriggerPolicy();
        var current = CreateQuant("ACCUMULATE", 100m, 75m);
        var last = new DateTimeOffset(2026, 9, 24, 7, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

        Assert.True(policy.ShouldAnalyze(
            current, null, Array.Empty<AiTriggerEvent>(), last, now,
            TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ShouldNotAnalyze_WhenNothingChanged()
    {
        var policy = new AiTriggerPolicy();
        var previous = CreateQuant("WATCH", 100m, 60m);
        var current = CreateQuant("WATCH", 100m, 60m);

        Assert.False(policy.ShouldAnalyze(
            current, previous, Array.Empty<AiTriggerEvent>()));
    }

    private static QuantAnalysisResult CreateQuant(string status, decimal price, decimal score)
    {
        var zone = new BuyZone(100m, 110m, 120m, 0m, status, Array.Empty<string>());
        var qs = new QuantScore(
            "TEST", 10m, 20m, 15m, 20m, 10m, score, AnalysisStatus.Watch);

        return new QuantAnalysisResult(
            qs,
            new DividendQualityMetrics(null, null, null, null, null, 0, 0),
            price, 3m, 2m, 10m, 11m, 12m, 10m, 10m,
            new FairValueRange(100m, 110m, 120m),
            0m, zone, Array.Empty<string>(), "READY");
    }
}
