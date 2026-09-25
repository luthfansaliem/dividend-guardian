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

        var message = TelegramMessageFormatter.Format(response, quant, usedFallback);
        await notifier.SendAsync(options.Value.BotToken, options.Value.ChatId, message, ct);
    }
}
