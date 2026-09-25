alter table ai_analysis
    add column if not exists run_id uuid;

create index if not exists ix_ai_analysis_run_id
    on ai_analysis(run_id);
