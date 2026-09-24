create table if not exists fundamental_ingestion_runs (
 id bigint generated always as identity primary key,
 provider varchar(50) not null,
 ticker varchar(10) not null references stocks(ticker),
 from_date date not null,
 to_date date not null,
 fundamental_rows int not null default 0,
 dividend_rows int not null default 0,
 status varchar(20) not null,
 error_message text,
 created_at timestamptz not null default now()
);
create index if not exists ix_fundamental_ingestion_ticker_time on fundamental_ingestion_runs(ticker, created_at desc);