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
    /// fiscal years. Median values are used to reduce sensitivity to one unusually
    /// high or low year. This is not a forecast and does not claim statistical
    /// normalization beyond the configured historical window.
    /// </summary>
    public static NormalizedValuationInputs Calculate(
        IReadOnlyList<AnnualFundamentalPoint> fundamentals,
        IReadOnlyList<AnnualDividendPoint> dividends,
        int years = 3)
    {
        if (years <= 0)
            throw new ArgumentOutOfRangeException(nameof(years));

        var orderedFundamentals = fundamentals
            .Where(x => x.Year > 0)
            .OrderBy(x => x.Year)
            .TakeLast(years)
            .ToArray();

        var orderedDividends = dividends
            .Where(x => x.Year > 0 && x.Dps > 0)
            .OrderBy(x => x.Year)
            .TakeLast(years)
            .ToArray();

        if (orderedFundamentals.Length == 0 && orderedDividends.Length == 0)
        {
            return new NormalizedValuationInputs(
                0, 0, 0, null, null, null);
        }

        var startYear = new[] {
            orderedFundamentals.Select(x => x.Year).DefaultIfEmpty(0).Min(),
            orderedDividends.Select(x => x.Year).DefaultIfEmpty(0).Min()
        }.Where(x => x > 0).Min();

        var endYear = new[] {
            orderedFundamentals.Select(x => x.Year).DefaultIfEmpty(0).Max(),
            orderedDividends.Select(x => x.Year).DefaultIfEmpty(0).Max()
        }.Max();

        var commonStartYear = new[] {
            orderedFundamentals.Select(x => x.Year).DefaultIfEmpty(0).Min(),
            orderedDividends.Select(x => x.Year).DefaultIfEmpty(0).Min()
        }.Where(x => x > 0).Max();

        var commonEndYear = new[] {
            orderedFundamentals.Select(x => x.Year).DefaultIfEmpty(0).Max(),
            orderedDividends.Select(x => x.Year).DefaultIfEmpty(0).Max()
        }.Where(x => x > 0).Min();

        var commonYearCount = commonStartYear > 0 && commonEndYear >= commonStartYear
            ? commonEndYear - commonStartYear + 1
            : Math.Max(orderedFundamentals.Length, orderedDividends.Length);

        return new NormalizedValuationInputs(
            commonYearCount,
            startYear,
            endYear,
            Median(orderedDividends.Select(x => x.Dps)),
            Median(orderedFundamentals.Select(x => x.Eps)),
            Median(orderedFundamentals
                .Where(x => x.SharesOutstanding > 0)
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
