namespace DividendGuardian.Notification;

public sealed record TelegramAlertData(
    string Ticker,
    string Verdict,
    decimal QuantScore,
    decimal CurrentPrice,
    decimal? ConservativeFairValue,
    decimal? BaseFairValue,
    decimal? MarginOfSafety,
    string DataQuality,
    string WhyAccumulate,
    string WhyNotAccumulate,
    IReadOnlyList<string> KeyRisks,
    IReadOnlyList<string> InvalidationTriggers,
    IReadOnlyList<string> DataGaps,
    bool UsedFallback);

public static class TelegramMessageFormatter
{
    public static string Format(TelegramAlertData data)
    {
        var lines = new List<string>
        {
            $"🔔 Dividend Guardian — {data.Ticker}",
            $"Status: {data.Verdict}",
            $"Quant Score: {data.QuantScore:F1}/100",
            $"Price: {data.CurrentPrice:F2}",
            $"Conservative FV: {FormatValue(data.ConservativeFairValue)}",
            $"Base FV: {FormatValue(data.BaseFairValue)}",
            $"Margin of Safety: {FormatPercent(data.MarginOfSafety)}",
            $"Data Quality: {data.DataQuality}",
            "",
            "WHY ACCUMULATE",
            data.WhyAccumulate,
            "",
            "WHY NOT ACCUMULATE",
            data.WhyNotAccumulate
        };

        AddSection(lines, "⚠️ RISKS", data.KeyRisks);
        AddSection(lines, "INVALIDATION", data.InvalidationTriggers);
        AddSection(lines, "DATA GAPS", data.DataGaps);

        if (data.UsedFallback)
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

    private static void AddSection(List<string> lines, string title, IReadOnlyList<string> values)
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
