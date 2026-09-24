create table if not exists market_data_ingestion_runs (
 id bigint generated always as identity primary key,
 provider varchar(50) not null,
 ticker varchar(10) not null references stocks(ticker),
 from_date date not null,
 to_date date not null,
 rows_written int not null default 0,
 status varchar(20) not null,
 error_message text,
 created_at timestamptz not null default now()
);
create index if not exists ix_market_data_ingestion_ticker_time on market_data_ingestion_runs(ticker, created_at desc);
