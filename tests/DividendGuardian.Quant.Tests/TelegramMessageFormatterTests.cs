using DividendGuardian.AI;
using DividendGuardian.Domain;
using DividendGuardian.Notification;
using DividendGuardian.Quant;

namespace DividendGuardian.Quant.Tests;

public sealed class TelegramMessageFormatterTests
{
    [Fact]
    public void Format_ContainsDecisionSupportFields()
    {
        var response = new AiAnalysisResponse(
            "ASII", "ACCUMULATE", "Yield and valuation support review.",
            "Cyclical exposure remains a risk.",
            new[] { "Debt can rise." },
            new[] { "No latest filing." },
            new[] { "Payout exceeds sustainable range." },
            "READY", "test-model", "DG-AI-TEST");

        var quant = CreateQuant();

        var message = TelegramMessageFormatter.Format(response, quant, false);

        Assert.Contains("ASII", message);
        Assert.Contains("Quant Score: 75.0/100", message);
        Assert.Contains("WHY ACCUMULATE", message);
        Assert.Contains("WHY NOT ACCUMULATE", message);
        Assert.Contains("RISKS", message);
        Assert.Contains("INVALIDATION", message);
        Assert.Contains("DATA GAPS", message);
        Assert.DoesNotContain("AI unavailable", message);
    }

    [Fact]
    public void Format_LabelsFallback()
    {
        var response = new AiAnalysisResponse(
            "ASII", "INSUFFICIENT DATA", "Deterministic output.",
            "AI interpretation unavailable.",
            Array.Empty<string>(), new[] { "AI unavailable" },
            new[] { "Re-run AI." }, "PARTIAL",
            "deterministic-fallback", "DG-AI-FALLBACK-1.0");

        var message = TelegramMessageFormatter.Format(response, CreateQuant(), true);

        Assert.Contains("AI unavailable", message);
        Assert.Contains("deterministic fallback", message);
    }

    private static QuantAnalysisResult CreateQuant()
    {
        var score = new QuantScore(
            "ASII", 10m, 20m, 15m, 20m, 10m, 75m, AnalysisStatus.Watch);
        var zone = new BuyZone(
            100m, 110m, 120m, 0.10m, "ACCUMULATE", Array.Empty<string>());

        return new QuantAnalysisResult(
            score,
            new DividendQualityMetrics(null, null, null, null, null, 0, 0),
            90m, 3m, 2m, 10m, 11m, 12m, 10m, 10m,
            new FairValueRange(100m, 110m, 120m),
            0.10m, zone, Array.Empty<string>(), "READY");
    }
}
