# Folder Map

src/DividendGuardian.Domain       Core contracts
src/DividendGuardian.Quant        Deterministic scoring, fair value, buy zone, allocation
src/DividendGuardian.Infrastructure Database and external data adapters
src/DividendGuardian.AI            AI analysis adapter
src/DividendGuardian.Notification  Telegram transport/formatting
src/DividendGuardian.Worker        Scheduler/orchestration
tests/DividendGuardian.Quant.Tests Quant regression tests
database/migrations                Versioned PostgreSQL schema
database/seed                       Initial watchlist
docs                                Architecture, scoring, runbooks