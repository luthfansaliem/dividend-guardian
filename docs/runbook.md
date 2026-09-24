# Local Runbook

## 1. Prerequisites
- .NET 10 SDK
- Supabase project
- PostgreSQL connection string

## 2. Database
Run in Supabase SQL Editor:
1. database/migrations/001_initial_schema.sql
2. database/seed/001_watchlist.sql

## 3. Environment
Set:
SUPABASE_CONNECTION_STRING
OPENAI_API_KEY (optional for BUILD 01)
OPENAI_MODEL (optional)
TELEGRAM_BOT_TOKEN (optional)
TELEGRAM_CHAT_ID (optional)

Never commit .env or secrets.

## 4. Verify
dotnet restore DividendGuardian.sln
dotnet build DividendGuardian.sln
dotnet test DividendGuardian.sln

## 5. Run
dotnet run --project src/DividendGuardian.Worker/DividendGuardian.Worker.csproj

BUILD 01 only performs database connectivity/watchlist checks.