alter table orcacore_inbox
    add column if not exists handoff_failure_count integer not null default 0,
    add column if not exists handoff_retry_not_before timestamp with time zone null;

create index if not exists ix_orcacore_inbox_received_order
    on orcacore_inbox (state, acceptance_sequence, event_id)
    include (handoff_failure_count, handoff_retry_not_before);

create index if not exists ix_orcacore_inbox_handoff_retry
    on orcacore_inbox (state, handoff_retry_not_before, acceptance_sequence, event_id)
    where handoff_failure_count > 0;
