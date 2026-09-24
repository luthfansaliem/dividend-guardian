namespace DividendGuardian.Infrastructure;

public sealed class NotConfiguredFundamentalDataProvider : IFundamentalDataProvider
{
    public Task<IReadOnlyCollection<FundamentalRecord>> GetAnnualFundamentalsAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct = default)
        => throw new InvalidOperationException("Fundamental data provider is not configured.");

    public Task<IReadOnlyCollection<DividendRecord>> GetDividendsAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct = default)
        => throw new InvalidOperationException("Fundamental data provider is not configured.");
}