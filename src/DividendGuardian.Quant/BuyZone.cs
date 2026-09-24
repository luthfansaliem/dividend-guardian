namespace DividendGuardian.Quant;

public sealed record BuyZone(decimal? ConservativeFairValue,decimal? BaseFairValue,decimal? OptimisticFairValue,decimal? MarginOfSafety,string Status);

public sealed class BuyZoneEngine
{
    public BuyZone Evaluate(decimal currentPrice,decimal? conservative,decimal? baseValue,decimal? optimistic,decimal quantScore)
    {
        if(currentPrice<=0||conservative is null||conservative<=0)
            return new BuyZone(conservative,baseValue,optimistic,null,"REVIEW");

        var mos=1m-currentPrice/conservative.Value;
        var status=mos>=.20m&&quantScore>=80 ? "STRONG_ACCUMULATE"
            : mos>=.10m&&quantScore>=70 ? "ACCUMULATE"
            : mos>=0 ? "WATCH"
            : "REVIEW";
        return new BuyZone(conservative,baseValue,optimistic,mos,status);
    }
}