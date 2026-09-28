namespace DividendGuardian.AI;

public enum AiAnalysisTrigger { PriceEnteredBuyZone, PriceDrop, NewFinancialReport, DividendAnnouncement, QuantScoreChanged, ManualReview }

public sealed record AiTriggerEvent(AiAnalysisTrigger Trigger, DateTimeOffset OccurredAt, string? Description = null);
