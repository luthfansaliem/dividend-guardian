namespace DividendGuardian.AI;

public sealed class AiOptions
{
    public string Provider { get; set; } = "openai";
    public string GroqApiKey { get; set; } = "";
    public string GroqModel { get; set; } = "openai/gpt-oss-20b";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-5.6-luna";
    public int MaxAnalysesPerDay { get; set; } = 10;
    public bool ForceAnalysis { get; set; }
    public int MaxRetries { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 500;
}
