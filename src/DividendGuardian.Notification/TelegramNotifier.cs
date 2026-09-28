using System.Net.Http.Json;

namespace DividendGuardian.Notification;

public sealed class TelegramNotifier
{
    private readonly HttpClient _http;

    public TelegramNotifier(HttpClient http) => _http = http;

    public async Task SendAsync(string botToken, string chatId, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            return;

        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
        using var response = await _http.PostAsJsonAsync(url, new { chat_id = chatId, text = message }, ct);
        response.EnsureSuccessStatusCode();
    }
}
