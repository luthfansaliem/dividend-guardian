namespace DividendGuardian.AI;

public sealed class AiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-5.6-luna";
    public int MaxAnalysesPerDay { get; set; } = 10;
}
