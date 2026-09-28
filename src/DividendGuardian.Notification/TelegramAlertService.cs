using Microsoft.Extensions.Options;

namespace DividendGuardian.Notification;

public sealed class TelegramAlertService(
    TelegramNotifier notifier,
    IOptions<TelegramOptions> options)
{
    public async Task SendAsync(
        TelegramAlertData data,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled ||
            string.IsNullOrWhiteSpace(options.Value.BotToken) ||
            string.IsNullOrWhiteSpace(options.Value.ChatId))
            return;

        var message = TelegramMessageFormatter.Format(data);
        await notifier.SendAsync(options.Value.BotToken, options.Value.ChatId, message, ct);
    }
}
