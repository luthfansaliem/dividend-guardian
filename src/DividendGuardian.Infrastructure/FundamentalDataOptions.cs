namespace DividendGuardian.Infrastructure;

public sealed class FundamentalDataOptions
{
    public string Provider { get; set; } = "none";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.twelvedata.com";
    public string MicCode { get; set; } = "XIDX";
    public int LookbackYears { get; set; } = 6;
    public int OutputSize { get; set; } = 6;
    public int RequestDelayMs { get; set; } = 500;
    public bool IncludeCashFlow { get; set; } = true;
    public bool IncludeBalanceSheet { get; set; } = true;
    public bool IncludeDividends { get; set; } = true;
}