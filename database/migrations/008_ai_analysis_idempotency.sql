alter table ai_analysis
    add column if not exists idempotency_key varchar(200);

create unique index if not exists ux_ai_analysis_idempotency_key
    on ai_analysis(idempotency_key)
    where idempotency_key is not null;

create index if not exists ix_ai_analysis_analysis_time
    on ai_analysis(analysis_time desc);
