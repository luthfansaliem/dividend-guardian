# BUILD 03 — Fundamentals & Dividend Engine

The engine stores annual financial statements and dividend history for the active watchlist.

## Provider

The first adapter is Twelve Data. Its XIDX exchange page exposes income statement, balance sheet, cash flow and dividend endpoints for Indonesian securities. Access to individual fundamentals endpoints depends on the Twelve Data subscription and endpoint entitlement, so the account must be checked before enabling production ingestion.

## Stored fields

Annual fundamentals:
- revenue
- net income
- EPS
- free cash flow
- equity
- total debt
- cash
- shares outstanding

Dividend history:
- annual DPS
- payment date
- payout ratio when available

## Scheduling

Fundamental ingestion is disabled by default. When enabled, the worker runs it on the first cycle and then at the configured interval (default 7 days), while EOD prices can continue to run daily.

## Cost control

Financial-statement endpoints can consume materially more API credits than EOD prices. Do not enable this blindly. For a low-cost MVP, keep the provider disabled until the Twelve Data account confirms XIDX access and expected monthly credits.

## Data quality

- Annual records are filtered to the requested date range.
- Provider failures are isolated per ticker.
- Upserts are idempotent.
- Each collection is recorded in fundamental_ingestion_runs.
- Provider-missing fields currently become zero in the normalized record. Before production scoring, add explicit nullable/raw-field handling so missing data cannot be confused with a true zero.

## Next BUILD 03.1

1. Calculate dividend payout ratio and FCF payout from stored data.
2. Calculate EPS CAGR and dividend growth/stability.
3. Create valuation snapshots from price + EPS + FCF.
4. Run deterministic Quant Engine.
5. Persist fair-value and buy-zone outputs.
