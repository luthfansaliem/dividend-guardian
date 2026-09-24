namespace DividendGuardian.Infrastructure;

public sealed record FundamentalRecord(
    string Ticker, DateOnly PeriodEnd, decimal Revenue, decimal NetIncome, decimal Eps,
    decimal FreeCashFlow, decimal Equity, decimal Debt, decimal Cash, long SharesOutstanding);

public sealed record DividendRecord(
    string Ticker, int FiscalYear, decimal Dps, DateOnly? PaymentDate, decimal? PayoutRatio);

public interface IFundamentalDataProvider
{
    Task<IReadOnlyCollection<FundamentalRecord>> GetAnnualFundamentalsAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyCollection<DividendRecord>> GetDividendsAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct = default);
}