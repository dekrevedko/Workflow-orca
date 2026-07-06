alter table orcacore_checkpoints
    add column if not exists runtime_state jsonb;
