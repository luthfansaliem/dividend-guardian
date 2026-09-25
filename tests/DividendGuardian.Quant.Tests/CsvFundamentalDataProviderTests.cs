using Microsoft.Extensions.Options;
using DividendGuardian.Infrastructure;

namespace DividendGuardian.Quant.Tests;

public sealed class CsvFundamentalDataProviderTests
{
    [Fact]
    public async Task ReadsFundamentalsAndDividendsFromCsv()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "fundamentals.csv"),
                """
                ticker,period_end,revenue,net_income,eps,free_cash_flow,equity,debt,cash,shares_outstanding
                TEST,2021-12-31,1000,100,10,80,500,200,50,100
                TEST,2022-12-31,1100,120,12,90,520,210,55,100
                OTHER,2022-12-31,999,99,9,80,400,100,40,100
                """);

            await File.WriteAllTextAsync(
                Path.Combine(directory, "dividends.csv"),
                """
                ticker,fiscal_year,dps,payment_date,payout_ratio
                TEST,2021,4,2022-04-01,40
                TEST,2022,5,2023-04-01,41.6667
                OTHER,2022,9,2023-04-01,100
                """);

            var options = Options.Create(new FundamentalDataOptions
            {
                CsvDirectory = directory,
                FundamentalsFileName = "fundamentals.csv",
                DividendsFileName = "dividends.csv"
            });
            var provider = new CsvFundamentalDataProvider(options);

            var fundamentals = await provider.GetAnnualFundamentalsAsync(
                "TEST", new DateOnly(2021, 1, 1), new DateOnly(2022, 12, 31));
            var dividends = await provider.GetDividendsAsync(
                "TEST", new DateOnly(2021, 1, 1), new DateOnly(2022, 12, 31));

            Assert.Equal(2, fundamentals.Count);
            Assert.Equal(12m, fundamentals.Last().Eps);
            Assert.Equal(2, dividends.Count);
            Assert.Equal(5m, dividends.Last().Dps);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
