# Dividend Guardian

Dividend Guardian is a decision-support system for a long-term Indonesian equity dividend portfolio (Dana Anak 2036).

## v1 goals

- Monitor a curated BEI watchlist.
- Store EOD prices, fundamentals and dividends.
- Calculate transparent quantitative scores.
- Estimate fair-value ranges and margin of safety.
- Use AI only when quantitative/event triggers require qualitative analysis.
- Notify through Telegram.
- Track four child portfolios.
- No automatic trading in v1.

## Architecture

Market Data -> .NET Worker -> Supabase PostgreSQL -> Quant Engine -> AI Analyst -> Decision Engine -> Telegram

## Watchlist

ASII, INDF, ICBP, TLKM, KLBF, JSMR, UNTR, PTBA, ITMG, PGAS

## Development

Requirements:
- .NET 10 SDK
- PostgreSQL/Supabase
- OpenAI API key (only required for AI features)
- Telegram bot token (only required for notifications)

Configuration uses environment variables. Never commit secrets.

See:
- `docs/architecture.md`
- `docs/scoring.md`
- `docs/data-sources.md`
- `database/migrations/001_initial_schema.sql`
