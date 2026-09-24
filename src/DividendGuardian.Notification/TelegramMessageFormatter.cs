namespace DividendGuardian.Notification;

public static class TelegramMessageFormatter
{
    public static string Format(string ticker,string status,decimal score,string summary)
        => $"🛡 DIVIDEND GUARDIAN\n\n{ticker} — {status}\nScore: {score:0.0}/100\n\n{summary}";
}