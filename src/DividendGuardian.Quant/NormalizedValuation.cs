namespace DividendGuardian.Quant;

public sealed record NormalizedValuationInputs(
    int YearCount,
    int StartYear,
    int EndYear,
    decimal? MedianDps,
    decimal? MedianEps,
    decimal? MedianFcfPerShare);

public static class NormalizedValuationEngine
{
    /// <summary>
    /// Calculates a simple, explicit normalized baseline from the latest completed
    /// fiscal years shared by fundamentals and dividends. Median values are used
    /// to reduce sensitivity to one unusually high or low year. This is not a
    /// forecast and does not claim statistical normalization beyond this window.
    /// </summary>
    public static NormalizedValuationInputs Calculate(
        IReadOnlyList<AnnualFundamentalPoint> fundamentals,
        IReadOnlyList<AnnualDividendPoint> dividends,
        int years = 3)
    {
        if (years <= 0)
            throw new ArgumentOutOfRangeException(nameof(years));

        var fundamentalByYear = fundamentals
            .Where(x => x.Year > 0)
            .GroupBy(x => x.Year)
            .ToDictionary(g => g.Key, g => g.Last());

        var dividendByYear = dividends
            .Where(x => x.Year > 0 && x.Dps > 0)
            .GroupBy(x => x.Year)
            .ToDictionary(g => g.Key, g => g.Last());

        var commonYears = fundamentalByYear.Keys
            .Intersect(dividendByYear.Keys)
            .OrderBy(x => x)
            .TakeLast(years)
            .ToArray();

        if (commonYears.Length == 0)
        {
            return new NormalizedValuationInputs(
                0, 0, 0, null, null, null);
        }

        var selectedFundamentals = commonYears
            .Select(year => fundamentalByYear[year])
            .ToArray();

        var selectedDividends = commonYears
            .Select(year => dividendByYear[year])
            .ToArray();

        return new NormalizedValuationInputs(
            commonYears.Length,
            commonYears[0],
            commonYears[^1],
            Median(selectedDividends.Select(x => x.Dps)),
            Median(selectedFundamentals.Select(x => x.Eps)),
            Median(selectedFundamentals
                .Where(x => x.SharesOutstanding > 0 && x.FreeCashFlow > 0)
                .Select(x => x.FreeCashFlow / x.SharesOutstanding)));
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
}
