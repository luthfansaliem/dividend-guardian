create index if not exists ix_quant_scores_ticker_date on quant_scores(ticker,analysis_date desc);
create index if not exists ix_ai_analysis_ticker_date on ai_analysis(ticker,analysis_date desc);
create index if not exists ix_alerts_ticker_created on alerts(ticker,created_at desc);
create index if not exists ix_transactions_child_date on portfolio_transactions(child_id,transaction_date desc);