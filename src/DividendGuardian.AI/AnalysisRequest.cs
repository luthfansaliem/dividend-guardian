namespace DividendGuardian.AI;

public sealed record AnalysisRequest(string Ticker,string QuantStatus,decimal QuantScore,string Facts,string Risks,string DataGaps);