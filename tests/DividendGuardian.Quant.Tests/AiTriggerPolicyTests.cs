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
    public void ShouldAnalyze_WhenPriceDropsAtLeastFivePercent()
    {
        var policy = new AiTriggerPolicy();
        var previous = CreateQuant("WATCH", 100m, 60m);
        var current = CreateQuant("WATCH", 95m, 60m);

        Assert.True(policy.ShouldAnalyze(
            current, previous, Array.Empty<AiTriggerEvent>()));
    }

    [Fact]
    public void ShouldAnalyze_WhenQuantScoreChangesAtLeastFivePoints()
    {
        var policy = new AiTriggerPolicy();
        var previous = CreateQuant("WATCH", 100m, 60m);
        var current = CreateQuant("WATCH", 100m, 65m);

        Assert.True(policy.ShouldAnalyze(
            current, previous, Array.Empty<AiTriggerEvent>()));
    }

    [Fact]
    public void ShouldNotAnalyze_WhenExternalTriggerIsInsideCooldown()
    {
        var policy = new AiTriggerPolicy();
        var current = CreateQuant("WATCH", 100m, 60m);
        var trigger = new AiTriggerEvent(
            AiAnalysisTrigger.NewFinancialReport,
            DateTimeOffset.UtcNow);
        var last = new DateTimeOffset(2026, 9, 25, 7, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

        Assert.False(policy.ShouldAnalyze(
            current, null, new[] { trigger }, last, now,
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

    [Fact]
    public void EvaluateTriggers_WhenEnteringBuyZone()
    {
        var policy = new AiTriggerPolicy();
        var previous = new AiTriggerState(100m, 60m, "WATCH");
        var current = CreateQuant("ACCUMULATE", 100m, 60m);
        var occurredAt = new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

        var triggers = policy.EvaluateTriggers(current, previous, occurredAt);

        var trigger = Assert.Single(triggers);
        Assert.Equal(AiAnalysisTrigger.PriceEnteredBuyZone, trigger.Trigger);
        Assert.Equal(occurredAt, trigger.OccurredAt);
    }

    [Fact]
    public void EvaluateTriggers_WhenPriceDropsAtLeastFivePercent()
    {
        var policy = new AiTriggerPolicy();
        var previous = new AiTriggerState(100m, 60m, "WATCH");
        var current = CreateQuant("WATCH", 95m, 60m);

        var triggers = policy.EvaluateTriggers(current, previous, DateTimeOffset.UtcNow);

        var trigger = Assert.Single(triggers);
        Assert.Equal(AiAnalysisTrigger.PriceDrop, trigger.Trigger);
    }

    [Fact]
    public void EvaluateTriggers_WhenScoreChangesAtLeastFivePoints()
    {
        var policy = new AiTriggerPolicy();
        var previous = new AiTriggerState(100m, 60m, "WATCH");
        var current = CreateQuant("WATCH", 100m, 65m);

        var triggers = policy.EvaluateTriggers(current, previous, DateTimeOffset.UtcNow);

        var trigger = Assert.Single(triggers);
        Assert.Equal(AiAnalysisTrigger.QuantScoreChanged, trigger.Trigger);
    }

    [Fact]
    public void ShouldAnalyzeFromState_DoesNotRepeatExistingBuyZone()
    {
        var policy = new AiTriggerPolicy();
        var previous = new AiTriggerState(100m, 75m, "ACCUMULATE");
        var current = CreateQuant("ACCUMULATE", 100m, 75m);

        var triggers = policy.EvaluateTriggers(current, previous, DateTimeOffset.UtcNow);

        Assert.Empty(triggers);
        Assert.False(policy.ShouldAnalyzeFromState(
            current, previous, triggers, null,
            DateTimeOffset.UtcNow, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ShouldAnalyzeFromState_WhenPriceDrops()
    {
        var policy = new AiTriggerPolicy();
        var previous = new AiTriggerState(100m, 75m, "WATCH");
        var current = CreateQuant("WATCH", 95m, 75m);

        var triggers = policy.EvaluateTriggers(current, previous, DateTimeOffset.UtcNow);

        Assert.Contains(triggers, x => x.Trigger == AiAnalysisTrigger.PriceDrop);
        Assert.True(policy.ShouldAnalyzeFromState(
            current, previous, triggers, null,
            DateTimeOffset.UtcNow, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ShouldAnalyzeFromState_WhenScoreChanges()
    {
        var policy = new AiTriggerPolicy();
        var previous = new AiTriggerState(100m, 60m, "WATCH");
        var current = CreateQuant("WATCH", 100m, 65m);

        var triggers = policy.EvaluateTriggers(current, previous, DateTimeOffset.UtcNow);

        Assert.Contains(triggers, x => x.Trigger == AiAnalysisTrigger.QuantScoreChanged);
        Assert.True(policy.ShouldAnalyzeFromState(
            current, previous, triggers, null,
            DateTimeOffset.UtcNow, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ShouldNotAnalyzeFromState_WhenNoPreviousAndNotInBuyZone()
    {
        var policy = new AiTriggerPolicy();
        var current = CreateQuant("WATCH", 100m, 60m);

        var triggers = policy.EvaluateTriggers(current, null, DateTimeOffset.UtcNow);

        Assert.Empty(triggers);
        Assert.False(policy.ShouldAnalyzeFromState(
            current, null, triggers, null,
            DateTimeOffset.UtcNow, TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ShouldNotAnalyzeFromState_WhenTriggeredButInsideCooldown()
    {
        var policy = new AiTriggerPolicy();
        var previous = new AiTriggerState(100m, 60m, "WATCH");
        var current = CreateQuant("WATCH", 95m, 60m);
        var triggers = policy.EvaluateTriggers(current, previous, DateTimeOffset.UtcNow);
        var last = new DateTimeOffset(2026, 9, 25, 7, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

        Assert.False(policy.ShouldAnalyzeFromState(
            current, previous, triggers, last, now,
            TimeSpan.FromHours(24)));
    }

    private static QuantAnalysisResult CreateQuant(string status, decimal price, decimal score)
    {
        var zone = new BuyZone(
            100m,
            110m,
            120m,
            0m,
            status,
            Array.Empty<string>());

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
