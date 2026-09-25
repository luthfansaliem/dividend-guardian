alter table ai_analysis
    add column if not exists data_quality varchar(30);

create index if not exists ix_ai_analysis_ticker_trigger_time
    on ai_analysis(ticker, trigger_type, analysis_time desc);
