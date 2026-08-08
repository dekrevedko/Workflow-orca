create table if not exists orcacore_pending_start_intents (
    start_idempotency_key text primary key,
    definition_id uuid not null,
    definition_version integer not null,
    workflow_input_content_type text not null,
    workflow_input_payload bytea not null,
    workflow_input_fingerprint text not null,
    state text not null,
    instance_id uuid null,
    definition_fingerprint text null,
    poison_code text null,
    poison_detail text null
);

create index if not exists ix_orcacore_inbox_pending_start
    on orcacore_inbox (start_idempotency_key, state, acceptance_sequence, event_id)
    where route_kind = 'start-or-deliver';
