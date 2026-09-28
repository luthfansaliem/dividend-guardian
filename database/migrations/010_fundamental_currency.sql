alter table fundamentals
    add column if not exists currency varchar(8);

comment on column fundamentals.currency is
    'Reporting currency supplied by the fundamental data provider, e.g. IDR or USD.';
