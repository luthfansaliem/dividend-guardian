namespace DividendGuardian.Domain;

public enum AnalysisStatus { Accumulate, Watch, Review, Avoid }

public sealed record Stock(
    string Ticker,
    string Name,
    string Sector,
    string? Subsector = null);

public sealed record FinancialSnapshot(
    string Ticker,
    DateOnly PeriodEnd,
    decimal Revenue,
    decimal NetIncome,
    decimal Eps,
    decimal FreeCashFlow,
    decimal Equity,
    decimal Debt,
    decimal Cash,
    long SharesOutstanding);

public sealed record DividendSnapshot(
    string Ticker,
    int FiscalYear,
    decimal Dps,
    decimal? PayoutRatio,
    DateOnly? PaymentDate);

public sealed record ValuationSnapshot(
    string Ticker,
    decimal Price,
    decimal DividendYield,
    decimal? Pe,
    decimal? FcfYield,
    decimal? Pb);

public sealed record QuantScore(
    string Ticker,
    decimal DividendYieldScore,
    decimal SustainabilityScore,
    decimal GrowthScore,
    decimal ValuationScore,
    decimal RiskScore,
    decimal TotalScore,
    AnalysisStatus Status);

public sealed record FairValueRange(
    decimal? Conservative,
    decimal? Base,
    decimal? Optimistic);

public sealed record PortfolioContribution(
    long ChildId,
    decimal Amount,
    DateOnly ContributionDate);
