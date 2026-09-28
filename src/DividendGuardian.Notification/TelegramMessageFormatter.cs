namespace DividendGuardian.Notification;

public sealed record TelegramAlertData(
    Guid RunId,
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
            $"🎯 ACTION: {GetAction(data)}",
            $"Next: {GetNextAction(data)}",
            "",
            $"Run ID: {data.RunId}",
            $"AI Assessment: {data.Verdict}",
            $"Quant Score: {data.QuantScore.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}/100",
            $"Price: {data.CurrentPrice:F2}",
            $"Conservative FV: {FormatValue(data.ConservativeFairValue)}",
            $"Base FV: {FormatValue(data.BaseFairValue)}",
            $"Margin of Safety: {FormatPercent(data.MarginOfSafety)}",
            $"Data Quality: {data.DataQuality}",
            "",
            "WHY?",
            BuildWhy(data),
            "",
            "AI — WHY ACCUMULATE",
            data.WhyAccumulate,
            "",
            "AI — WHY NOT ACCUMULATE",
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

    private static string GetAction(TelegramAlertData data)
    {
        if (data.DataQuality.Equals("INCONSISTENT", StringComparison.OrdinalIgnoreCase))
            return "REVIEW DATA — jangan gunakan alert ini untuk keputusan investasi";

        if (data.ConservativeFairValue is null || data.BaseFairValue is null)
            return "WAIT — fair value belum cukup reliable";

        if (data.MarginOfSafety is < 0)
            return "WAIT — harga belum masuk conservative value";

        return data.QuantScore >= 70m
            ? "REVIEW OPPORTUNITY — cek thesis sebelum keputusan"
            : "WAIT — Quant belum cukup kuat";
    }

    private static string GetNextAction(TelegramAlertData data)
    {
        if (data.DataQuality.Equals("INCONSISTENT", StringComparison.OrdinalIgnoreCase))
            return "Validasi fundamental/unit data lalu jalankan Quant ulang.";
        if (data.ConservativeFairValue is null || data.BaseFairValue is null)
            return "Lengkapi baseline valuasi lalu jalankan analisis ulang.";
        if (data.MarginOfSafety is < 0)
            return "Pantau sampai valuasi/margin of safety membaik.";
        return "Review WHY/risks di bawah sebelum mengambil keputusan.";
    }

    private static string BuildWhy(TelegramAlertData data)
    {
        if (data.DataQuality.Equals("INCONSISTENT", StringComparison.OrdinalIgnoreCase))
            return "Input fundamental gagal sanity check; metrik valuation/payout yang bergantung pada unit tersebut tidak dipercaya.";
        if (data.ConservativeFairValue is null || data.BaseFairValue is null)
            return "Fair value belum tersedia sehingga harga belum dapat dibandingkan dengan valuation range secara memadai.";
        if (data.MarginOfSafety is < 0)
            return "Harga berada di atas conservative fair value.";
        return $"Quant score {data.QuantScore:F1}/100 dengan data quality {data.DataQuality}.";
    }

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
