using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DividendGuardian.AI;

public sealed class AiAnalyst(HttpClient httpClient,AiOptions options)
{
    public async Task<string?> AnalyzeAsync(string input,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(options.ApiKey)) return null;
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/responses");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",options.ApiKey);
        request.Content=JsonContent.Create(new {
            model=options.Model,
            input=new object[] {
                new { role="system", content=DividendGuardianAiPrompt.System },
                new { role="user", content=input }
            }
        });
        using var response=await httpClient.SendAsync(request,ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}