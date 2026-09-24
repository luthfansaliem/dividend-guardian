# Dividend Guardian v1 Architecture

Market Data -> Worker -> Supabase PostgreSQL -> Quant Engine -> AI Analyst -> Decision Engine -> Telegram.

## Project boundaries
- Domain: immutable investment contracts.
- Quant: deterministic scoring and fair-value primitives.
- Infrastructure: PostgreSQL/Supabase access.
- AI: qualitative analysis; never overrides quant score.
- Notification: Telegram transport.
- Worker: scheduling and orchestration.

## Secrets
Use environment variables. Never commit API keys, tokens, broker credentials or OTPs.

## v1 safety
No broker integration and no automatic order execution.