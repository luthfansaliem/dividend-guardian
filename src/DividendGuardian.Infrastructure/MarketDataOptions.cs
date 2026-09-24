namespace DividendGuardian.Infrastructure;

public sealed class MarketDataOptions
{
    public string Provider { get; set; } = "none";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.twelvedata.com";
    public string MicCode { get; set; } = "XIDX";
    public int LookbackDays { get; set; } = 14;
    public int RequestDelayMs { get; set; } = 250;
}
