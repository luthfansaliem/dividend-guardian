namespace DividendGuardian.Infrastructure;

public sealed record EodPrice(string Ticker,DateOnly TradeDate,decimal Open,decimal High,decimal Low,decimal Close,long Volume);

public interface IMarketDataProvider
{
    Task<IReadOnlyList<EodPrice>> GetEodPricesAsync(string ticker,DateOnly from,DateOnly to,CancellationToken ct=default);
}

public sealed class NotConfiguredMarketDataProvider : IMarketDataProvider
{
    public Task<IReadOnlyList<EodPrice>> GetEodPricesAsync(string ticker,DateOnly from,DateOnly to,CancellationToken ct=default)
        => throw new InvalidOperationException("No market-data provider is configured. Add a provider whose terms permit automated retrieval.");
}