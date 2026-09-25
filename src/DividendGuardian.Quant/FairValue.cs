using DividendGuardian.Domain;

namespace DividendGuardian.Quant;

public sealed record FairValueInput(
    decimal? ForwardDps,
    decimal? NormalizedEps,
    decimal? NormalizedFcfPerShare,
    decimal? HistoricalMedianDividendYieldPercent,
    decimal? HistoricalMedianPe,
    decimal? HistoricalMedianFcfYieldPercent);

public sealed record FairValueResult(
    FairValueRange Range,
    decimal? DividendYieldFairValue,
    decimal? PeFairValue,
    decimal? FcfFairValue,
    IReadOnlyList<string> Reasons);

public sealed class FairValueEngine
{
    public FairValueResult Calculate(
        FairValueInput input,
        decimal conservativeMultiplier = 0.90m,
        decimal optimisticMultiplier = 1.10m)
    {
        ValidateMultiplier(conservativeMultiplier, nameof(conservativeMultiplier));
        ValidateMultiplier(optimisticMultiplier, nameof(optimisticMultiplier));

        var dividend = DividendYieldFairValue(
            input.ForwardDps, input.HistoricalMedianDividendYieldPercent);
        var pe = PeFairValue(input.NormalizedEps, input.HistoricalMedianPe);
        var fcf = FcfFairValue(
            input.NormalizedFcfPerShare, input.HistoricalMedianFcfYieldPercent);

        var methods = new[] { dividend, pe, fcf }
            .Where(x => x is > 0)
            .Select(x => x!.Value)
            .ToArray();

        if (methods.Length == 0)
        {
            return new FairValueResult(
                new FairValueRange(null, null, null),
                dividend, pe, fcf,
                new[] { "Fair value unavailable: no valid valuation method has sufficient data." });
        }

        var baseValue = Median(methods)!.Value;
        var conservative = baseValue * conservativeMultiplier;
        var optimistic = baseValue * optimisticMultiplier;

        var reasons = new List<string>
        {
            $"Fair value uses {methods.Length} independent valuation method(s).",
            $"Base fair value {baseValue:F2}; range {conservative:F2}-{optimistic:F2}."
        };

        if (dividend is not null)
            reasons.Add($"Dividend-yield fair value {dividend.Value:F2}.");
        if (pe is not null)
            reasons.Add($"PE fair value {pe.Value:F2}.");
        if (fcf is not null)
            reasons.Add($"FCF-yield fair value {fcf.Value:F2}.");

        return new FairValueResult(
            new FairValueRange(conservative, baseValue, optimistic),
            dividend, pe, fcf, reasons);
    }

    public static decimal? DividendYieldFairValue(
        decimal? forwardDps, decimal? targetYieldPercent) =>
        forwardDps is > 0 && targetYieldPercent is > 0
            ? forwardDps.Value / (targetYieldPercent.Value / 100m)
            : null;

    public static decimal? PeFairValue(decimal? normalizedEps, decimal? targetPe) =>
        normalizedEps is > 0 && targetPe is > 0
            ? normalizedEps.Value * targetPe.Value
            : null;

    public static decimal? FcfFairValue(
        decimal? normalizedFcfPerShare, decimal? targetFcfYieldPercent) =>
        normalizedFcfPerShare is > 0 && targetFcfYieldPercent is > 0
            ? normalizedFcfPerShare.Value / (targetFcfYieldPercent.Value / 100m)
            : null;

    public static decimal? MarginOfSafety(
        decimal currentPrice, decimal? conservativeFairValue)
    {
        if (currentPrice <= 0 || conservativeFairValue is null || conservativeFairValue <= 0)
            return null;

        return 1m - currentPrice / conservativeFairValue.Value;
    }

    private static decimal? Median(IEnumerable<decimal> values)
    {
        var ordered = values.Where(x => x > 0).OrderBy(x => x).ToArray();
        if (ordered.Length == 0) return null;

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2m
            : ordered[middle];
    }

    private static void ValidateMultiplier(decimal value, string name)
    {
        if (value <= 0 || value > 1.5m)
            throw new ArgumentOutOfRangeException(
                name, "Multiplier must be > 0 and <= 1.5.");
    }
}
