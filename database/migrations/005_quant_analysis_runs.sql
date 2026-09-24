create table if not exists quant_analysis_runs (
 id bigint generated always as identity primary key,
 analysis_date date not null,
 ticker varchar(10) not null references stocks(ticker),
 status varchar(30) not null,
 total_score numeric(6,2),
 analysis_status varchar(30),
 data_quality varchar(30) not null,
 error_message text,
 created_at timestamptz not null default now()
);

create index if not exists ix_quant_analysis_runs_ticker_date
on quant_analysis_runs(ticker, analysis_date desc);

create index if not exists ix_quant_analysis_runs_date
on quant_analysis_runs(analysis_date desc);
