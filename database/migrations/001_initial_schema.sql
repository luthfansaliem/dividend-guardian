create table if not exists stocks (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null unique,
 name varchar(200) not null,
 sector varchar(100),
 subsector varchar(100),
 is_active boolean not null default true,
 created_at timestamptz not null default now()
);
create table if not exists daily_prices (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null references stocks(ticker),
 trade_date date not null,
 open_price numeric(18,4), high_price numeric(18,4), low_price numeric(18,4),
 close_price numeric(18,4), adjusted_close numeric(18,4), volume bigint,
 created_at timestamptz not null default now(),
 unique(ticker, trade_date)
);
create table if not exists fundamentals (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null references stocks(ticker),
 period_end date not null,
 revenue numeric(24,4), net_income numeric(24,4), eps numeric(20,6),
 free_cash_flow numeric(24,4), equity numeric(24,4), total_debt numeric(24,4),
 cash numeric(24,4), shares_outstanding bigint,
 created_at timestamptz not null default now(),
 unique(ticker, period_end)
);
create table if not exists dividends (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null references stocks(ticker),
 fiscal_year int not null,
 dps numeric(18,4) not null, payment_date date, payout_ratio numeric(10,4),
 created_at timestamptz not null default now(),
 unique(ticker, fiscal_year)
);
create table if not exists valuations (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null references stocks(ticker),
 valuation_date date not null,
 price numeric(18,4) not null,
 dividend_yield numeric(10,4), pe numeric(12,4), pb numeric(12,4),
 ev_ebitda numeric(12,4), fcf_yield numeric(10,4),
 created_at timestamptz not null default now(),
 unique(ticker, valuation_date)
);
create table if not exists quant_scores (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null references stocks(ticker),
 analysis_date date not null,
 dividend_yield_score numeric(6,2), sustainability_score numeric(6,2),
 growth_score numeric(6,2), valuation_score numeric(6,2), risk_score numeric(6,2),
 total_score numeric(6,2), status varchar(30) not null,
 model_version varchar(30) not null default 'DG-1.0',
 created_at timestamptz not null default now(),
 unique(ticker, analysis_date)
);
create table if not exists fair_values (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null references stocks(ticker),
 analysis_date date not null,
 conservative_value numeric(18,4), base_value numeric(18,4), optimistic_value numeric(18,4),
 margin_of_safety numeric(10,6),
 model_version varchar(30) not null default 'DG-1.0',
 created_at timestamptz not null default now(),
 unique(ticker, analysis_date)
);
create table if not exists news_events (
 id bigint generated always as identity primary key,
 ticker varchar(10) references stocks(ticker), event_time timestamptz,
 severity varchar(20), title text not null, source_url text, content_excerpt text,
 materiality varchar(20), processed boolean not null default false,
 created_at timestamptz not null default now()
);
create table if not exists ai_analysis (
 id bigint generated always as identity primary key,
 ticker varchar(10) not null references stocks(ticker),
 analysis_time timestamptz not null default now(), trigger_type varchar(50) not null,
 model varchar(100) not null, status varchar(30), summary text,
 why_accumulate text, why_not_accumulate text, risks text,
 invalidation_triggers text, data_gaps text, confidence numeric(6,4),
 raw_response jsonb, model_version varchar(30) not null default 'DG-1.0'
);
create table if not exists portfolio_children (
 id bigint generated always as identity primary key,
 name varchar(100) not null, birth_date date, target_date date,
 active boolean not null default true
);
create table if not exists portfolio_transactions (
 id bigint generated always as identity primary key,
 child_id bigint not null references portfolio_children(id),
 ticker varchar(10) not null references stocks(ticker),
 transaction_date date not null, transaction_type varchar(20) not null,
 shares int not null default 0, price numeric(18,4) not null default 0,
 fees numeric(18,4) not null default 0, total_amount numeric(18,4) not null default 0,
 notes text
);
create table if not exists alerts (
 id bigint generated always as identity primary key,
 ticker varchar(10), alert_type varchar(50) not null, severity varchar(20) not null,
 message text not null, sent_at timestamptz, created_at timestamptz not null default now()
);
create index if not exists ix_prices_ticker_date on daily_prices(ticker, trade_date desc);
create index if not exists ix_fundamentals_ticker_period on fundamentals(ticker, period_end desc);
create index if not exists ix_dividends_ticker_year on dividends(ticker, fiscal_year desc);
create index if not exists ix_news_ticker_time on news_events(ticker, event_time desc);
create index if not exists ix_ai_ticker_time on ai_analysis(ticker, analysis_time desc);

insert into stocks (ticker,name,sector,subsector) values
('ASII','Astra International','Industrials','Diversified'),
('INDF','Indofood Sukses Makmur','Consumer Staples','Food'),
('ICBP','Indofood CBP Sukses Makmur','Consumer Staples','Food'),
('TLKM','Telkom Indonesia','Communication Services','Telecommunications'),
('KLBF','Kalbe Farma','Healthcare','Pharmaceuticals'),
('JSMR','Jasa Marga','Industrials','Infrastructure'),
('UNTR','United Tractors','Industrials','Heavy Equipment'),
('PTBA','Bukit Asam','Energy','Coal'),
('ITMG','Indo Tambangraya Megah','Energy','Coal'),
('PGAS','Perusahaan Gas Negara','Energy','Gas')
on conflict (ticker) do nothing;