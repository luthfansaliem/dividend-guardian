# Dividend Guardian v1 Architecture

Market Data -> .NET Worker -> PostgreSQL/Supabase -> Quant Engine -> Fair Value -> AI Analyst -> Decision Engine -> Telegram.

v1 deliberately excludes automatic broker orders, broker credentials, ML training and intraday trading.

Components:
- Domain: models and statuses.
- Quant: deterministic scoring and valuation.
- Infrastructure: PostgreSQL and data adapters.
- AI: qualitative analysis.
- Notification: Telegram.
- Worker: orchestration and scheduling.

Data flow must remain auditable: raw data -> calculated metrics -> score -> decision -> AI explanation -> alert.
