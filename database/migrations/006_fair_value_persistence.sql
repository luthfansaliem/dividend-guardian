-- BUILD 04.2: ensure fair value persistence is available for existing databases.
create table if not exists fair_values (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null references stocks(ticker),
 analysis_date date not null,
 conservative_value numeric(18,4),
 base_value numeric(18,4),
 optimistic_value numeric(18,4),
 margin_of_safety numeric(10,6),
 model_version varchar(30) not null default 'DG-1.0',
 created_at timestamptz not null default now(),
 unique(ticker, analysis_date)
);

create index if not exists ix_fair_values_ticker_date
on fair_values(ticker, analysis_date desc);
