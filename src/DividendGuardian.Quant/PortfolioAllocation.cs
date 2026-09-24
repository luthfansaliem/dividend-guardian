namespace DividendGuardian.Quant;

public sealed record AllocationTarget(string Ticker,decimal Percentage,decimal Amount);

public sealed class PortfolioAllocationEngine
{
    public IReadOnlyList<AllocationTarget> Allocate(decimal monthlyBudget,IReadOnlyList<(string Ticker,decimal Score)> candidates)
    {
        if(monthlyBudget<=0||candidates.Count==0) return [];
        var eligible=candidates.Where(x=>x.Score>=70).OrderByDescending(x=>x.Score).Take(4).ToArray();
        if(eligible.Length==0) return [];
        var totalWeight=eligible.Sum(x=>Math.Max(0,x.Score-60));
        return eligible.Select(x=>{
            var pct=Math.Round(Math.Max(0,x.Score-60)/totalWeight,4);
            return new AllocationTarget(x.Ticker,pct,Math.Round(monthlyBudget*pct,2));
        }).ToArray();
    }
}