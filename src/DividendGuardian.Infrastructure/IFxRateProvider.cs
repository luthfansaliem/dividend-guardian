namespace DividendGuardian.Infrastructure;

public sealed record FxRatePoint(int Year, decimal Rate);

public interface IFxRateProvider
{
    Task<IReadOnlyDictionary<int, decimal>> GetYearEndRatesAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
}
