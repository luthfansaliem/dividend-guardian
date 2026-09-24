namespace DividendGuardian.Quant;

public sealed class FairValueEngine
{
    public decimal? DividendYieldFairValue(decimal forwardDps, decimal targetYieldPercent) =>
        forwardDps > 0 && targetYieldPercent > 0
            ? forwardDps / (targetYieldPercent / 100m)
            : null;

    public decimal? PeFairValue(decimal normalizedEps, decimal targetPe) =>
        normalizedEps > 0 && targetPe > 0 ? normalizedEps * targetPe : null;

    public static decimal? MarginOfSafety(decimal currentPrice, decimal? conservativeFairValue)
    {
        if (currentPrice <= 0 || conservativeFairValue is null || conservativeFairValue <= 0)
            return null;
        return 1m - (currentPrice / conservativeFairValue.Value);
    }
}
