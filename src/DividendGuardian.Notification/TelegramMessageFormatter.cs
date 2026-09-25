using DividendGuardian.AI;
using DividendGuardian.Quant;

namespace DividendGuardian.Notification;

public static class TelegramMessageFormatter
{
    public static string Format(
        AiAnalysisResponse response,
        QuantAnalysisResult quant,
        bool usedFallback)
    {
        var lines = new List<string>
        {
            $"🔔 Dividend Guardian — {response.Ticker}",
            $"Status: {response.Verdict}",
            $"Quant Score: {quant.Score.TotalScore:F1}/100",
            $"Price: {quant.CurrentPrice:F2}",
            $"Conservative FV: {FormatValue(quant.FairValue.Conservative)}",
            $"Base FV: {FormatValue(quant.FairValue.Base)}",
            $"Margin of Safety: {FormatPercent(quant.MarginOfSafety)}",
            $"Data Quality: {quant.DataQuality}",
            "",
            "WHY ACCUMULATE",
            response.WhyAccumulate,
            "",
            "WHY NOT ACCUMULATE",
            response.WhyNotAccumulate
        };

        AddSection(lines, "⚠️ RISKS", response.KeyRisks);
        AddSection(lines, "INVALIDATION", response.InvalidationTriggers);
        AddSection(lines, "DATA GAPS", response.DataGaps);

        if (usedFallback)
        {
            lines.Add("");
            lines.Add("ℹ️ AI unavailable; this message uses the deterministic fallback.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string Format(string ticker, string status, decimal score, string summary) =>
        $"🛡 DIVIDEND GUARDIAN{Environment.NewLine}{Environment.NewLine}" +
        $"{ticker} — {status}{Environment.NewLine}" +
        $"Score: {score:0.0}/100{Environment.NewLine}{Environment.NewLine}{summary}";

    private static void AddSection(
        List<string> lines,
        string title,
        IReadOnlyList<string> values)
    {
        if (values.Count == 0) return;
        lines.Add("");
        lines.Add(title);
        lines.AddRange(values.Take(5).Select(x => $"• {x}"));
    }

    private static string FormatValue(decimal? value) =>
        value is null ? "N/A" : value.Value.ToString("F2");

    private static string FormatPercent(decimal? value) =>
        value is null ? "N/A" : value.Value.ToString("P1");
}
