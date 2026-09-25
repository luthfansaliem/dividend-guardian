namespace DividendGuardian.AI;

public sealed class AiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-5.6-luna";
    public int MaxAnalysesPerDay { get; set; } = 10;
    public int MaxRetries { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 500;
}
