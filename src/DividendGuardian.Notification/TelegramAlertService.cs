using Microsoft.Extensions.Options;

namespace DividendGuardian.Notification;

public sealed class TelegramAlertService(
    TelegramNotifier notifier,
    IOptions<TelegramOptions> options)
{
    public async Task SendAsync(
        DividendGuardian.AI.AiAnalysisResponse response,
        DividendGuardian.Quant.QuantAnalysisResult quant,
        bool usedFallback,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled ||
            string.IsNullOrWhiteSpace(options.Value.BotToken) ||
            string.IsNullOrWhiteSpace(options.Value.ChatId))
            return;

        var data = new TelegramAlertData(
            response.Ticker,
            response.Verdict,
            quant.Score.TotalScore,
            quant.CurrentPrice,
            quant.FairValue.Conservative,
            quant.FairValue.Base,
            quant.MarginOfSafety,
            quant.DataQuality,
            response.WhyAccumulate,
            response.WhyNotAccumulate,
            response.KeyRisks,
            response.InvalidationTriggers,
            response.DataGaps,
            usedFallback);

        var message = TelegramMessageFormatter.Format(data);
        await notifier.SendAsync(options.Value.BotToken, options.Value.ChatId, message, ct);
    }
}
