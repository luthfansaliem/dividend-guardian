namespace DividendGuardian.Notification;

public sealed class TelegramOptions
{
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
    public bool Enabled { get; set; }
}
