using DividendGuardian.Notification;

namespace DividendGuardian.Quant.Tests;

public sealed class TelegramMessageFormatterTests
{
    [Fact]
    public void Format_ContainsDecisionSupportFields()
    {
        var message = TelegramMessageFormatter.Format(CreateData());

        Assert.Contains("ASII", message);
        Assert.Contains("Run ID: 11111111-1111-1111-1111-111111111111", message);
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
        var message = TelegramMessageFormatter.Format(CreateData() with
        {
            Verdict = "INSUFFICIENT DATA",
            UsedFallback = true
        });

        Assert.Contains("AI unavailable", message);
        Assert.Contains("deterministic fallback", message);
    }

    private static TelegramAlertData CreateData() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "ASII", "ACCUMULATE", 75m, 90m, 100m, 110m, 0.10m, "READY",
            "Yield and valuation support review.",
            "Cyclical exposure remains a risk.",
            new[] { "Debt can rise." },
            new[] { "Payout exceeds sustainable range." },
            new[] { "No latest filing." },
            false);
}
